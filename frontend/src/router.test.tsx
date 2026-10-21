// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "./auth/useSessao";
import { ToastProvider } from "./components/ui/Toast";
import { rotas } from "./router";
import { responderApi } from "./test/apiFalsa";

const sessaoDe = (perfil: string): ContextoSessao => ({
  estado: "autenticado",
  usuario: { id: "u1", nome: "Sonia Alves", email: "s@nap.demo", perfil, cursoId: null, curso: null, deveTrocarSenha: false },
  saiuPorVontade: false,
  entrar: vi.fn(),
  trocarSenha: vi.fn(),
  sair: vi.fn(),
});
const sessao: ContextoSessao = {
  estado: "autenticado",
  usuario: { id: "u1", nome: "Sonia Alves", email: "s@nap.demo", perfil: "Sip", cursoId: null, curso: null, deveTrocarSenha: false },
  saiuPorVontade: false,
  entrar: vi.fn(),
  trocarSenha: vi.fn(),
  sair: vi.fn(),
};

describe("router", () => {
  it("Rota_notificacoes_redireciona_para_a_aba", async () => {
    responderApi({
      "/notificacoes/contagem": { naoLidas: 0 },
      "/comunicacoes/contagem": { naoLidas: 0 },
      "/notificacoes": { itens: [], pagina: 1, tamanho: 20, total: 0 },
    });
    const router = createMemoryRouter(rotas, { initialEntries: ["/notificacoes"] });
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <SessaoContexto value={sessao}>
          <ToastProvider>
            <RouterProvider router={router} />
          </ToastProvider>
        </SessaoContexto>
      </QueryClientProvider>,
    );

    expect(await screen.findByRole("tab", { name: "Notificações", selected: true })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/comunicacoes");
    expect(router.state.location.search).toBe("?aba=notificacoes");
  });

  it("Gestao_recebe_acesso_negado_em_comunicacoes", async () => {
    responderApi({});
    const router = createMemoryRouter(rotas, { initialEntries: ["/comunicacoes"] });
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <SessaoContexto value={sessaoDe("Gestao")}>
          <ToastProvider>
            <RouterProvider router={router} />
          </ToastProvider>
        </SessaoContexto>
      </QueryClientProvider>,
    );
    expect(await screen.findByText("Acesso negado")).toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "Entrada" })).not.toBeInTheDocument();
  });

  it("Com_troca_pendente_toda_rota_leva_a_definir_senha", async () => {
    responderApi({});
    const pendente: ContextoSessao = { ...sessaoDe("ProfissionalNap"), usuario: { ...sessaoDe("ProfissionalNap").usuario!, deveTrocarSenha: true } };
    for (const destino of ["/", "/alunos", "/administracao"]) {
      const router = createMemoryRouter(rotas, { initialEntries: [destino] });
      const { unmount } = render(
        <QueryClientProvider client={new QueryClient()}>
          <SessaoContexto value={pendente}>
            <ToastProvider>
              <RouterProvider router={router} />
            </ToastProvider>
          </SessaoContexto>
        </QueryClientProvider>,
      );
      expect(await screen.findByRole("heading", { name: "Defina uma nova senha" })).toBeInTheDocument();
      expect(router.state.location.pathname).toBe("/definir-senha");
      expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
      unmount();
    }
  });

  it("Redirecionamento_nao_afeta_assinar", async () => {
    const pendente: ContextoSessao = { ...sessaoDe("ProfissionalNap"), usuario: { ...sessaoDe("ProfissionalNap").usuario!, deveTrocarSenha: true } };
    const router = createMemoryRouter(rotas, { initialEntries: ["/assinar/tok"] });
    render(
      <QueryClientProvider client={new QueryClient()}>
        <SessaoContexto value={pendente}>
          <RouterProvider router={router} />
        </SessaoContexto>
      </QueryClientProvider>,
    );
    expect(await screen.findByLabelText("Digite o código de 6 dígitos")).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/assinar/tok");
  });

  it("Administracao_so_para_administrador_e_coordenacao_nap", async () => {
    responderApi({ "/cursos": [], "/notificacoes/contagem": { naoLidas: 0 }, "/comunicacoes/contagem": { naoLidas: 0 }, "/usuarios": { itens: [], pagina: 1, tamanho: 20, total: 0 } });
    const router = createMemoryRouter(rotas, { initialEntries: ["/administracao"] });
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <SessaoContexto value={sessaoDe("ProfissionalNap")}>
          <ToastProvider>
            <RouterProvider router={router} />
          </ToastProvider>
        </SessaoContexto>
      </QueryClientProvider>,
    );
    expect(await screen.findByText("Acesso negado")).toBeInTheDocument();
  });

  it("Rota_de_assinatura_abre_sem_sessao_e_sem_menu", async () => {
    const router = createMemoryRouter(rotas, { initialEntries: ["/assinar/tok"] });
    render(
      <QueryClientProvider client={new QueryClient()}>
        <SessaoContexto value={{ ...sessao, estado: "anonimo", usuario: null }}>
          <RouterProvider router={router} />
        </SessaoContexto>
      </QueryClientProvider>,
    );
    expect(await screen.findByLabelText("Digite o código de 6 dígitos")).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/assinar/tok");
    expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
  });
});
