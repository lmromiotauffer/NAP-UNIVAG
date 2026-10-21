// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../../../components/ui/Toast";
import { administracaoService } from "../services/administracaoService";
import { AlertasTab } from "./AlertasTab";

vi.mock("../services/administracaoService", () => ({ administracaoService: { obterAlertas: vi.fn(), salvarAlertas: vi.fn() } }));

const LIMITES = { faltasConsecutivas: 3, faltasJanela: 3, janelaDias: 30, diasSemAcolhimento: 7, diasSemAtualizacao: 15, diasPeiPendente: 15 };

function montar() {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ToastProvider>
        <AlertasTab />
      </ToastProvider>
    </QueryClientProvider>,
  );
  return userEvent.setup();
}

beforeEach(() => {
  vi.mocked(administracaoService.obterAlertas).mockResolvedValue({ ...LIMITES, versao: "v1" });
  vi.mocked(administracaoService.salvarAlertas).mockReset();
});

describe("AlertasTab", () => {
  it("Alertas_fora_da_faixa_mostram_erro_no_campo", async () => {
    const user = montar();
    const campo = await screen.findByLabelText(/^Faltas consecutivas/);
    await user.clear(campo);
    await user.type(campo, "25");
    await user.click(screen.getByRole("button", { name: "Salvar limites" }));

    expect(await screen.findByText("Informe um valor de 1 a 20.")).toBeInTheDocument();
    expect(campo).toHaveAttribute("aria-invalid", "true");
    expect(administracaoService.salvarAlertas).not.toHaveBeenCalled();
  });

  it("Salvar_envia_a_versao_lida_e_avisa", async () => {
    vi.mocked(administracaoService.salvarAlertas).mockResolvedValue({ ...LIMITES, diasSemAtualizacao: 21, versao: "v2" });
    const user = montar();
    const campo = await screen.findByLabelText(/^Dias sem atualização/);
    await user.clear(campo);
    await user.type(campo, "21");
    await user.click(screen.getByRole("button", { name: "Salvar limites" }));

    expect(await screen.findByText("Limites de alerta salvos.")).toBeInTheDocument();
    expect(administracaoService.salvarAlertas).toHaveBeenCalledWith({ ...LIMITES, diasSemAtualizacao: 21 }, "v1");
  });
});
