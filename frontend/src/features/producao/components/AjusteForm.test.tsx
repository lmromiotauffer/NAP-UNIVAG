// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { responderApi } from "../../../test/apiFalsa";
import { producaoService } from "../services/producaoService";
import { AjusteForm } from "./AjusteForm";

vi.mock("../services/producaoService", () => ({ producaoService: { criarAjuste: vi.fn() } }));
const criar = vi.mocked(producaoService.criarAjuste);

function montar(aoConcluir = vi.fn()) {
  responderApi({ "/usuarios/profissionais": [{ id: "p1", nome: "Ana Souza" }] });
  render(
    <QueryClientProvider client={new QueryClient()}>
      <AjusteForm aoConcluir={aoConcluir} aoCancelar={vi.fn()} />
    </QueryClientProvider>,
  );
  return { user: userEvent.setup(), aoConcluir };
}

async function preencherBasico(user: ReturnType<typeof userEvent.setup>) {
  await screen.findByRole("option", { name: "Ana Souza" });
  await user.selectOptions(screen.getByLabelText(/^Profissional/), "p1");
  await user.selectOptions(screen.getByLabelText(/^Categoria/), "ReunioesEquipe");
}

beforeEach(() => criar.mockReset());

describe("AjusteForm", () => {
  it("Ajuste_exige_valor_e_justificativa", async () => {
    const { user } = montar();
    await preencherBasico(user);
    await user.click(screen.getByRole("button", { name: "Registrar ajuste" }));
    expect(await screen.findByText("Informe ocorrências ou minutos diferentes de zero.")).toBeInTheDocument();
    expect(screen.getByText(/Informe a justificativa/)).toBeInTheDocument();
    expect(criar).not.toHaveBeenCalled();
  });

  it("Ajuste_aceita_negativo_nao_oferece_ociosidade_e_envia_a_chave", async () => {
    criar.mockResolvedValueOnce({ id: "a1" });
    const { user, aoConcluir } = montar();
    expect(screen.queryByRole("option", { name: "Horários disponíveis sem utilização" })).not.toBeInTheDocument();
    await preencherBasico(user);
    await user.type(screen.getByLabelText(/^Minutos/), "-30");
    await user.type(screen.getByLabelText(/^Justificativa/), "Reunião cancelada");
    await user.click(screen.getByRole("button", { name: "Registrar ajuste" }));
    await waitFor(() => expect(aoConcluir).toHaveBeenCalled());
    expect(criar).toHaveBeenCalledWith(expect.objectContaining({ profissionalId: "p1", categoria: "ReunioesEquipe", ocorrencias: null, minutos: -30, justificativa: "Reunião cancelada" }), expect.any(String));
  });

  it("Ajuste_em_semana_fechada_mostra_o_erro", async () => {
    criar.mockRejectedValueOnce({ status: 422, titulo: "Regra de negócio", detalhe: "A semana está fechada; peça a reabertura à coordenação.", errosCampo: {} });
    const { user, aoConcluir } = montar();
    await preencherBasico(user);
    await user.type(screen.getByLabelText(/^Ocorrências/), "2");
    await user.type(screen.getByLabelText(/^Justificativa/), "Reunião não lançada");
    await user.click(screen.getByRole("button", { name: "Registrar ajuste" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("A semana está fechada; peça a reabertura à coordenação.");
    expect(aoConcluir).not.toHaveBeenCalled();
  });
});
