// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../../../components/ui/Toast";
import type { Notificacao } from "../../../lib/types";
import { notificacaoService } from "../../notificacoes/services/notificacaoService";
import { AbaNotificacoes } from "./AbaNotificacoes";

vi.mock("../../notificacoes/services/notificacaoService", () => ({
  notificacaoService: { listar: vi.fn(), contagem: vi.fn(), marcarLida: vi.fn(), marcarTodasLidas: vi.fn() },
}));

const servico = vi.mocked(notificacaoService);
const nova: Notificacao = { id: "n1", tipo: "encaminhamento-criado", titulo: "Novo encaminhamento", texto: "Ana Souza foi encaminhada ao SIP.", link: "/encaminhamentos/e1", criadaEm: "2026-10-02T12:00:00Z", lida: false };

function montar() {
  const router = createMemoryRouter(
    [
      { path: "/comunicacoes", element: <AbaNotificacoes /> },
      { path: "/encaminhamentos/:id", element: <p>Detalhe do encaminhamento</p> },
    ],
    { initialEntries: ["/comunicacoes?aba=notificacoes"] },
  );
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <ToastProvider>
        <RouterProvider router={router} />
      </ToastProvider>
    </QueryClientProvider>,
  );
  return userEvent.setup();
}

beforeEach(() => {
  vi.resetAllMocks();
  servico.listar.mockResolvedValue({ itens: [nova], pagina: 1, tamanho: 20, total: 1 });
  servico.marcarLida.mockResolvedValue(undefined);
  servico.marcarTodasLidas.mockResolvedValue(undefined);
});

describe("AbaNotificacoes", () => {
  it("Abrir_notificacao_marca_lida_e_navega", async () => {
    const user = montar();
    await user.click(await screen.findByRole("button", { name: "Abrir: Novo encaminhamento" }));

    await waitFor(() => expect(screen.getByText("Detalhe do encaminhamento")).toBeInTheDocument());
    expect(servico.marcarLida).toHaveBeenCalledWith("n1");
  });

  it("Lista_mostra_texto_fixo_e_marca_todas_como_lidas", async () => {
    const user = montar();
    expect(await screen.findByText("Ana Souza foi encaminhada ao SIP.")).toBeInTheDocument();
    expect(screen.getByText("Ler uma notificação não equivale a dar ciência.")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Marcar todas como lidas" }));
    await waitFor(() => expect(servico.marcarTodasLidas).toHaveBeenCalled());
  });

  it("Sem_notificacoes_mostra_estado_vazio", async () => {
    servico.listar.mockResolvedValue({ itens: [], pagina: 1, tamanho: 20, total: 0 });
    montar();
    expect(await screen.findByText("Nenhuma notificação")).toBeInTheDocument();
  });

  it("Falha_da_lista_oferece_tentar_novamente", async () => {
    servico.listar.mockRejectedValue(new Error("falhou"));
    montar();
    expect(await screen.findByRole("button", { name: /tentar novamente/i })).toBeInTheDocument();
  });
});
