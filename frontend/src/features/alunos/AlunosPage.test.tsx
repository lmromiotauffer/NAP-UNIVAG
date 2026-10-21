// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../auth/useSessao";
import { ToastProvider } from "../../components/ui/Toast";
import { responderApi } from "../../test/apiFalsa";
import { AlunosPage } from "./AlunosPage";
import { alunoService } from "./services/alunoService";

vi.mock("./services/alunoService", () => ({
  alunoService: { listar: vi.fn().mockResolvedValue({ itens: [], pagina: 1, tamanho: 20, total: 0 }), criar: vi.fn() },
}));
responderApi({
  "/cursos": [{ id: "11111111-1111-1111-1111-111111111111", nome: "Direito", ativo: true }],
  "/usuarios/profissionais": [{ id: "22222222-2222-2222-2222-222222222222", nome: "Ana Souza", perfil: "ProfissionalNap" }],
});

const sessao: ContextoSessao = {
  estado: "autenticado",
  usuario: { id: "u", nome: "Helena Prado", email: "h@nap.demo", perfil: "CoordenadorNap", cursoId: null, curso: null, deveTrocarSenha: false },
  saiuPorVontade: false,
  entrar: vi.fn(),
  trocarSenha: vi.fn(),
  sair: vi.fn(),
};

function montar(perfil = sessao.usuario) {
  const router = createMemoryRouter([{ path: "/alunos", element: <AlunosPage /> }], { initialEntries: ["/alunos"] });
  render(
    <QueryClientProvider client={new QueryClient()}>
      <SessaoContexto value={{ ...sessao, usuario: perfil }}>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </SessaoContexto>
    </QueryClientProvider>,
  );
  return { router, user: userEvent.setup() };
}

describe("AlunosPage", () => {
  it("busca_preserva_espaco_digitado_apos_o_debounce", async () => {
    const { router, user } = montar();
    const busca = screen.getByLabelText("Buscar por nome ou matrícula");
    await user.type(busca, "Maria ");
    await waitFor(() => expect(router.state.location.search).toBe("?busca=Maria"));
    await user.type(busca, "Clara");
    expect(busca).toHaveValue("Maria Clara");
    await waitFor(() => expect(router.state.location.search).toBe("?busca=Maria+Clara"));
    expect(busca).toHaveValue("Maria Clara");
  });

  it("Filtro_de_tipo_de_encaminhamento_envia_o_parametro", async () => {
    const { router, user } = montar();
    await user.selectOptions(screen.getByLabelText("Tipo de encaminhamento"), "Psicologia");
    await waitFor(() => expect(router.state.location.search).toBe("?especialidadeEncaminhamento=Psicologia"));
    await waitFor(() =>
      expect(alunoService.listar).toHaveBeenLastCalledWith(expect.objectContaining({ especialidadeEncaminhamento: "Psicologia" })),
    );
  });

  it("Coordenador_de_curso_so_ve_a_opcao_de_coordenacao_do_curso_no_filtro_de_encaminhamento", () => {
    montar({ ...sessao.usuario!, perfil: "CoordenadorCurso", cursoId: "11111111-1111-1111-1111-111111111111", curso: "Direito" });
    const opcoes = within(screen.getByLabelText("Tipo de encaminhamento")).getAllByRole("option");
    expect(opcoes.map((o) => o.textContent)).toEqual(["Encaminhamento: todos", "Coordenação do curso"]);
  });

  it("modal_de_cadastro_fica_travado_enquanto_salva", async () => {
    vi.mocked(alunoService.criar).mockReturnValue(new Promise(() => {}));
    const { user } = montar();
    await user.click((await screen.findAllByRole("button", { name: "Cadastrar aluno" }))[0]!);
    const dialogo = screen.getByRole("dialog");
    const d = within(dialogo);
    await user.type(d.getByLabelText(/Nome completo/), "Maria Clara Souza");
    await user.type(d.getByLabelText(/Matrícula/), "2026.0001");
    await user.selectOptions(d.getByLabelText(/Curso/), await d.findByRole("option", { name: "Direito" }));
    await user.type(d.getByLabelText(/Semestre/), "3");
    await user.type(d.getByLabelText(/Início do acompanhamento/), "2026-03-02");
    await user.selectOptions(d.getByLabelText(/Profissional responsável/), await d.findByRole("option", { name: "Ana Souza" }));
    await user.click(d.getByRole("button", { name: "Salvar" }));

    await waitFor(() => expect(d.getByRole("button", { name: "Fechar" })).toBeDisabled());
    await user.keyboard("{Escape}");
    await user.click(dialogo);
    expect(d.getByLabelText(/Nome completo/)).toHaveValue("Maria Clara Souza");
  });
});
