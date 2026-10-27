// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../../../components/ui/Toast";
import { administracaoService } from "../services/administracaoService";
import { FeriadosTab } from "./FeriadosTab";

vi.mock("../services/administracaoService", () => ({ administracaoService: { listarFeriados: vi.fn(), criarFeriado: vi.fn(), inativarFeriado: vi.fn() } }));

function montar() {
  const router = createMemoryRouter([{ path: "/administracao", element: <FeriadosTab /> }], { initialEntries: ["/administracao"] });
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ToastProvider>
        <RouterProvider router={router} />
      </ToastProvider>
    </QueryClientProvider>,
  );
  return userEvent.setup();
}

beforeEach(() => {
  vi.mocked(administracaoService.listarFeriados).mockResolvedValue([{ id: "f1", data: "2026-09-07", descricao: "Independência" }]);
  vi.mocked(administracaoService.criarFeriado).mockReset();
  vi.mocked(administracaoService.inativarFeriado).mockReset();
});

describe("FeriadosTab", () => {
  it("Feriado_mostra_conflitos_apos_criar", async () => {
    vi.mocked(administracaoService.criarFeriado).mockResolvedValue({
      id: "f2",
      conflitos: [
        {
          id: "c1",
          tipo: "Atendimento",
          titulo: "Atendimento: Letícia Ramos",
          profissionalId: "p1",
          profissional: "Ana Souza",
          inicio: "2026-11-02T09:00:00-04:00",
          fim: "2026-11-02T10:00:00-04:00",
        },
      ],
    });
    const user = montar();
    await user.click(await screen.findByRole("button", { name: "Novo feriado" }));
    const d = within(screen.getByRole("dialog"));
    await user.type(d.getByLabelText(/^Data/), "2026-11-02");
    await user.type(d.getByLabelText(/^Descrição/), "Finados");
    await user.click(d.getByRole("button", { name: "Salvar" }));

    expect(administracaoService.criarFeriado).toHaveBeenCalledWith({ data: "2026-11-02", descricao: "Finados" });
    const conflitos = within(await screen.findByRole("dialog", { name: "Feriado cadastrado com conflitos" }));
    expect(conflitos.getByText(/Nada foi apagado\. Resolva cada compromisso na agenda\./)).toBeInTheDocument();
    expect(conflitos.getByText("Atendimento: Letícia Ramos")).toBeInTheDocument();
    expect(conflitos.getByRole("link", { name: /Ver na agenda/ })).toHaveAttribute("href", "/agenda?semana=2026-11-02&profissional=p1");
  });

  it("Feriado_sem_conflitos_so_avisa", async () => {
    vi.mocked(administracaoService.criarFeriado).mockResolvedValue({ id: "f2", conflitos: [] });
    const user = montar();
    await user.click(await screen.findByRole("button", { name: "Novo feriado" }));
    const d = within(screen.getByRole("dialog"));
    await user.type(d.getByLabelText(/^Data/), "2026-11-02");
    await user.type(d.getByLabelText(/^Descrição/), "Finados");
    await user.click(d.getByRole("button", { name: "Salvar" }));

    expect(await screen.findByText("Feriado cadastrado.")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("Remover_pede_confirmacao_e_inativa", async () => {
    vi.mocked(administracaoService.inativarFeriado).mockResolvedValue(undefined);
    const user = montar();
    await user.click(await screen.findByRole("button", { name: "Remover Independência" }));
    expect(administracaoService.inativarFeriado).not.toHaveBeenCalled();
    await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Remover" }));
    expect(administracaoService.inativarFeriado).toHaveBeenCalledWith("f1");
  });
});
