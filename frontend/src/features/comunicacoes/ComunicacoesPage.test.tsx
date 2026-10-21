// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../auth/useSessao";
import { ToastProvider } from "../../components/ui/Toast";
import type { MensagemLista } from "../../lib/types";
import { notificacaoService } from "../notificacoes/services/notificacaoService";
import { ComunicacoesPage } from "./ComunicacoesPage";
import { comunicacaoService } from "./services/comunicacaoService";

vi.mock("./services/comunicacaoService", () => ({
  comunicacaoService: { caixa: vi.fn(), contagem: vi.fn(), destinatarios: vi.fn() },
}));
vi.mock("../notificacoes/services/notificacaoService", () => ({
  notificacaoService: { listar: vi.fn(), contagem: vi.fn(), marcarLida: vi.fn(), marcarTodasLidas: vi.fn() },
}));

const servico = vi.mocked(comunicacaoService);
const notificacoes = vi.mocked(notificacaoService);
const mensagem: MensagemLista = {
  id: "m1",
  tipo: "Aviso",
  assunto: "Reunião de acompanhamento",
  remetente: "Ana Souza",
  destinatario: "Carlos Mendes",
  alunoId: "a1",
  aluno: "Maria Clara",
  data: "2026-10-01T13:00:00Z",
  lida: false,
  recebida: true,
};
const sessao: ContextoSessao = {
  estado: "autenticado",
  usuario: { id: "u1", nome: "Carlos Mendes", email: "c@nap.demo", perfil: "ProfissionalNap", cursoId: null, curso: null, deveTrocarSenha: false },
  saiuPorVontade: false,
  entrar: vi.fn(),
  trocarSenha: vi.fn(),
  sair: vi.fn(),
};

function montar(url: string) {
  const router = createMemoryRouter([{ path: "/comunicacoes", element: <ComunicacoesPage /> }], { initialEntries: [url] });
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <SessaoContexto value={sessao}>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </SessaoContexto>
    </QueryClientProvider>,
  );
  return { router, user: userEvent.setup() };
}

beforeEach(() => {
  vi.resetAllMocks();
  servico.caixa.mockResolvedValue({ itens: [mensagem], pagina: 1, tamanho: 20, total: 1 });
  notificacoes.listar.mockResolvedValue({ itens: [], pagina: 1, tamanho: 20, total: 0 });
});

describe("ComunicacoesPage", () => {
  it("Abas_seguem_a_url", async () => {
    const { router, user } = montar("/comunicacoes?aba=enviados");
    expect(await screen.findByRole("tab", { name: "Enviados", selected: true })).toBeInTheDocument();
    await waitFor(() => expect(servico.caixa).toHaveBeenCalledWith(expect.objectContaining({ caixa: "Enviados" })));

    await user.click(screen.getByRole("tab", { name: "Rascunhos" }));
    expect(router.state.location.search).toBe("?aba=rascunhos");
    await waitFor(() => expect(servico.caixa).toHaveBeenCalledWith(expect.objectContaining({ caixa: "Rascunhos" })));

    // Teclado WAI-ARIA: seta para a direita leva à próxima aba.
    await user.keyboard("{ArrowRight}");
    expect(router.state.location.search).toBe("?aba=arquivadas");
    expect(screen.getByRole("tab", { name: "Arquivadas" })).toHaveFocus();

    await user.click(screen.getByRole("tab", { name: "Notificações" }));
    expect(router.state.location.search).toBe("?aba=notificacoes");
    expect(await screen.findByText("Nenhuma notificação")).toBeInTheDocument();
  });

  it("Entrada_e_a_aba_padrao_e_lista_a_mensagem", async () => {
    montar("/comunicacoes");
    expect(await screen.findByRole("tab", { name: "Entrada", selected: true })).toBeInTheDocument();
    expect(await screen.findByRole("link", { name: /Reunião de acompanhamento/ })).toHaveAttribute("href", "/comunicacoes/m1");
    expect(servico.caixa).toHaveBeenCalledWith(expect.objectContaining({ caixa: "Entrada", pagina: 1 }));
  });

  it("Caixa_vazia_mostra_estado_vazio", async () => {
    servico.caixa.mockResolvedValue({ itens: [], pagina: 1, tamanho: 20, total: 0 });
    montar("/comunicacoes");
    expect(await screen.findByText("Nenhuma mensagem")).toBeInTheDocument();
  });

  it("Falha_da_caixa_oferece_tentar_novamente", async () => {
    servico.caixa.mockRejectedValue(new Error("falhou"));
    montar("/comunicacoes");
    expect(await screen.findByRole("button", { name: /tentar novamente/i })).toBeInTheDocument();
  });
});
