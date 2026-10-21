// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../../auth/useSessao";
import { ToastProvider } from "../../../components/ui/Toast";
import { responderApi } from "../../../test/apiFalsa";
import { administracaoService } from "../services/administracaoService";
import { UsuariosTab } from "./UsuariosTab";

vi.mock("../services/administracaoService", () => ({
  administracaoService: { listarUsuarios: vi.fn(), gerarSenhaTemporaria: vi.fn(), criarUsuario: vi.fn(), situacaoUsuario: vi.fn(), atualizarUsuario: vi.fn() },
}));

responderApi({ "/cursos": [{ id: "c1", nome: "Direito", ativo: true, versao: "v1" }] });

const usuarioAdmin = (id: string, nome: string, perfil: string) => ({ id, nome, email: `${id}@nap.demo`, perfil, cursoId: null, curso: null, ativo: true, deveTrocarSenha: false, versao: `v-${id}` });
const LISTA = [usuarioAdmin("adm", "Ada Admin", "Administrador"), usuarioAdmin("pro", "Rafael Torres", "ProfissionalNap"), usuarioAdmin("coord", "Helena Prado", "CoordenadorNap")];

const sessaoDe = (id: string, perfil: string): ContextoSessao => ({
  estado: "autenticado",
  usuario: { id, nome: "Quem Opera", email: "q@nap.demo", perfil, cursoId: null, curso: null, deveTrocarSenha: false },
  saiuPorVontade: false,
  entrar: vi.fn(),
  trocarSenha: vi.fn(),
  sair: vi.fn(),
});

let cliente: QueryClient;

function montar(perfil: string) {
  cliente = new QueryClient();
  const router = createMemoryRouter([{ path: "/administracao", element: <UsuariosTab /> }], { initialEntries: ["/administracao"] });
  render(
    <QueryClientProvider client={cliente}>
      <SessaoContexto value={sessaoDe("coord", perfil)}>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </SessaoContexto>
    </QueryClientProvider>,
  );
  return userEvent.setup();
}

beforeEach(() => {
  localStorage.clear();
  vi.mocked(administracaoService.listarUsuarios).mockResolvedValue({ itens: LISTA, pagina: 1, tamanho: 20, total: LISTA.length });
});

describe("UsuariosTab", () => {
  it("Coordenacao_nap_nao_ve_perfil_administrador", async () => {
    const user = montar("CoordenadorNap");
    await user.click(await screen.findByRole("button", { name: "Novo usuário" }));
    const perfil = within(screen.getByRole("dialog")).getByLabelText(/Perfil/);
    const opcoes = within(perfil).getAllByRole("option").map((o) => o.textContent);
    expect(opcoes).not.toContain("Administração");
    expect(opcoes).toContain("Profissional NAP");
    // Também não administra administradores já cadastrados.
    expect(screen.queryByRole("button", { name: "Editar Ada Admin" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Editar Rafael Torres" })).toBeInTheDocument();
  });

  it("Administrador_ve_o_perfil_administrador_e_o_curso_so_para_coordenador_de_curso", async () => {
    const user = montar("Administrador");
    await user.click(await screen.findByRole("button", { name: "Novo usuário" }));
    const d = within(screen.getByRole("dialog"));
    expect(within(d.getByLabelText(/Perfil/)).getAllByRole("option").map((o) => o.textContent)).toContain("Administração");
    expect(d.queryByLabelText(/Curso/)).not.toBeInTheDocument();
    await user.selectOptions(d.getByLabelText(/Perfil/), "CoordenadorCurso");
    expect(d.getByLabelText(/Curso/)).toBeInTheDocument();
  });

  it("Senha_temporaria_aparece_uma_vez_e_nao_persiste", async () => {
    vi.mocked(administracaoService.gerarSenhaTemporaria).mockResolvedValue({ usuarioId: "pro", senhaTemporaria: "Kq7mTzP4wXn9HbVd" });
    const user = montar("CoordenadorNap");
    await user.click(await screen.findByRole("button", { name: "Gerar senha temporária para Rafael Torres" }));
    await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Gerar senha" }));

    expect(await screen.findByText("Kq7mTzP4wXn9HbVd")).toBeInTheDocument();
    expect(within(screen.getByRole("dialog")).getByText("pro@nap.demo")).toBeInTheDocument();
    expect(JSON.stringify({ ...localStorage })).not.toContain("Kq7mTzP4wXn9HbVd");
    expect(JSON.stringify({ ...sessionStorage })).not.toContain("Kq7mTzP4wXn9HbVd");

    await user.click(screen.getByRole("button", { name: "Concluir" }));
    expect(screen.queryByText("Kq7mTzP4wXn9HbVd")).not.toBeInTheDocument();
    // Nem o cache de mutações guarda a senha (gcTime 0 + reset ao fechar).
    expect(cliente.getMutationCache().getAll().filter((m) => JSON.stringify(m.state.data ?? null).includes("Kq7mTzP4wXn9HbVd"))).toHaveLength(0);
  });

  it("Inativar_pede_confirmacao_e_avisa_do_encerramento_das_sessoes", async () => {
    vi.mocked(administracaoService.situacaoUsuario).mockResolvedValue(undefined);
    const user = montar("CoordenadorNap");
    await user.click(await screen.findByRole("button", { name: "Inativar Rafael Torres" }));
    const d = within(screen.getByRole("dialog"));
    expect(d.getByText(/Isso encerra imediatamente as sessões abertas desta pessoa\./)).toBeInTheDocument();
    await user.click(d.getByRole("button", { name: "Inativar" }));
    expect(administracaoService.situacaoUsuario).toHaveBeenCalledWith("pro", { ativo: false }, "v-pro");
  });

  it("Ninguem_inativa_nem_redefine_a_propria_senha_pela_lista", async () => {
    montar("CoordenadorNap");
    expect(await screen.findByRole("button", { name: "Editar Helena Prado" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Inativar Helena Prado" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Gerar senha temporária para Helena Prado" })).not.toBeInTheDocument();
  });
});
