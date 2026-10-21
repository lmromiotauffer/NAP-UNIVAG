// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { responderApi } from "../../../test/apiFalsa";
import { alunoService } from "../services/alunoService";
import { AlunoForm } from "./AlunoForm";

vi.mock("../services/alunoService", () => ({ alunoService: { criar: vi.fn(), atualizar: vi.fn() } }));
responderApi({
  "/cursos": [{ id: "11111111-1111-1111-1111-111111111111", nome: "Direito", ativo: true }],
  "/usuarios/profissionais": [{ id: "22222222-2222-2222-2222-222222222222", nome: "Ana Souza", perfil: "ProfissionalNap" }],
});

const criar = vi.mocked(alunoService.criar);

function montar() {
  const aoConcluir = vi.fn();
  render(
    <QueryClientProvider client={new QueryClient()}>
      <AlunoForm aoConcluir={aoConcluir} />
    </QueryClientProvider>,
  );
  return { aoConcluir, user: userEvent.setup() };
}

async function preencher(user: ReturnType<typeof userEvent.setup>, semestre = "3") {
  await user.type(screen.getByLabelText(/Nome completo/), "Maria Clara Souza");
  await user.type(screen.getByLabelText(/Matrícula/), "2026.0001");
  await user.selectOptions(screen.getByLabelText(/Curso/), await screen.findByRole("option", { name: "Direito" }));
  if (semestre) await user.type(screen.getByLabelText(/Semestre/), semestre);
  await user.type(screen.getByLabelText(/Início do acompanhamento/), "2026-03-02");
  await user.selectOptions(screen.getByLabelText(/Profissional responsável/), await screen.findByRole("option", { name: "Ana Souza" }));
}

beforeEach(() => {
  criar.mockReset();
});

describe("AlunoForm", () => {
  it("erros_do_servidor_aparecem_nos_campos_e_preenchimento_e_mantido", async () => {
    criar.mockRejectedValueOnce({ status: 400, titulo: "Dados inválidos", errosCampo: { matricula: ["A matrícula já está em uso."] } });
    const { user, aoConcluir } = montar();
    await preencher(user);
    await user.click(screen.getByRole("button", { name: "Salvar" }));

    const matricula = screen.getByLabelText(/Matrícula/);
    await waitFor(() => expect(matricula).toHaveAttribute("aria-invalid", "true"));
    expect(screen.getByText("A matrícula já está em uso.")).toBeInTheDocument();
    expect(screen.getByLabelText(/Nome completo/)).toHaveValue("Maria Clara Souza");
    expect(matricula).toHaveValue("2026.0001");
    expect(aoConcluir).not.toHaveBeenCalled();
  });

  it("semestre_vazio_nao_vira_zero", async () => {
    const { user } = montar();
    await preencher(user, "");
    await user.click(screen.getByRole("button", { name: "Salvar" }));

    expect(await screen.findByText("Informe o semestre.")).toBeInTheDocument();
    expect(criar).not.toHaveBeenCalled();
  });

  it("duplo_clique_envia_um_unico_post_com_a_mesma_chave", async () => {
    criar.mockReturnValue(new Promise(() => {}));
    const { user } = montar();
    await preencher(user);
    // Dois cliques no mesmo tick, antes de o React re-renderizar o botão desabilitado.
    const salvar = screen.getByRole("button", { name: "Salvar" });
    fireEvent.click(salvar);
    fireEvent.click(salvar);

    await waitFor(() => expect(criar).toHaveBeenCalledTimes(1));
    await new Promise((r) => setTimeout(r, 50));
    expect(criar).toHaveBeenCalledTimes(1);
    const [corpo, chave] = criar.mock.calls[0] ?? [];
    expect(corpo).toMatchObject({ semestre: 3, matricula: "2026.0001" });
    expect(chave).toMatch(/^[0-9a-f-]{36}$/);
  });

  it("retentativa_apos_erro_reusa_a_chave", async () => {
    criar.mockRejectedValueOnce({ status: 503, titulo: "Servidor indisponível", errosCampo: {} }).mockResolvedValueOnce({ id: "33333333-3333-3333-3333-333333333333" });
    const { user, aoConcluir } = montar();
    await preencher(user);
    await user.click(screen.getByRole("button", { name: "Salvar" }));

    expect(await screen.findByText("Servidor indisponível")).toBeInTheDocument();
    expect(aoConcluir).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "Salvar" }));

    await waitFor(() => expect(aoConcluir).toHaveBeenCalledWith("33333333-3333-3333-3333-333333333333"));
    expect(criar).toHaveBeenCalledTimes(2);
    expect(criar.mock.calls[1]?.[1]).toBe(criar.mock.calls[0]?.[1]);
  });

  it("na_edicao_erro_de_campo_oculto_vai_para_o_alerta", async () => {
    vi.mocked(alunoService.atualizar).mockRejectedValueOnce({ status: 400, titulo: "Dados inválidos", errosCampo: { matricula: ["Matrícula inválida."] } });
    const aluno = {
      id: "33333333-3333-3333-3333-333333333333",
      nome: "Maria Clara Souza",
      matricula: "2026.0001",
      cursoId: "11111111-1111-1111-1111-111111111111",
      curso: "Direito",
      semestre: 3,
      profissionalId: "22222222-2222-2222-2222-222222222222",
      profissional: "Ana Souza",
      situacao: "Ativo" as const,
      inicioAcompanhamento: "2026-03-02",
      necessidadesEducacionais: null,
      observacoesAcademicas: null,
      resumo: null,
      versao: "v7",
    };
    render(
      <QueryClientProvider client={new QueryClient()}>
        <AlunoForm aluno={aluno} aoConcluir={vi.fn()} />
      </QueryClientProvider>,
    );
    await screen.findByRole("option", { name: "Ana Souza" });
    await userEvent.setup().click(screen.getByRole("button", { name: "Salvar" }));
    expect(await screen.findByText("Matrícula inválida.")).toBeInTheDocument();
  });

  it("na_edicao_envia_a_versao_lida_da_ficha", async () => {
    vi.mocked(alunoService.atualizar).mockResolvedValueOnce(undefined);
    const aluno = {
      id: "33333333-3333-3333-3333-333333333333",
      nome: "Maria Clara Souza",
      matricula: "2026.0001",
      cursoId: "11111111-1111-1111-1111-111111111111",
      curso: "Direito",
      semestre: 3,
      profissionalId: "22222222-2222-2222-2222-222222222222",
      profissional: "Ana Souza",
      situacao: "Ativo" as const,
      inicioAcompanhamento: "2026-03-02",
      necessidadesEducacionais: null,
      observacoesAcademicas: null,
      resumo: null,
      versao: "v7",
    };
    const aoConcluir = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <AlunoForm aluno={aluno} aoConcluir={aoConcluir} />
      </QueryClientProvider>,
    );
    await screen.findByRole("option", { name: "Ana Souza" });
    await userEvent.setup().click(screen.getByRole("button", { name: "Salvar" }));
    await waitFor(() => expect(aoConcluir).toHaveBeenCalled());
    expect(alunoService.atualizar).toHaveBeenCalledWith(aluno.id, expect.any(Object), aluno.versao);
  });

  it("apos_409_a_retentativa_continua_enviando_a_versao_de_quando_o_formulario_abriu", async () => {
    vi.mocked(alunoService.atualizar).mockReset();
    vi.mocked(alunoService.atualizar).mockRejectedValueOnce({ status: 409, titulo: "Conflito", errosCampo: {} }).mockResolvedValueOnce(undefined);
    const aluno = {
      id: "33333333-3333-3333-3333-333333333333",
      nome: "Maria Clara Souza",
      matricula: "2026.0001",
      cursoId: "11111111-1111-1111-1111-111111111111",
      curso: "Direito",
      semestre: 3,
      profissionalId: "22222222-2222-2222-2222-222222222222",
      profissional: "Ana Souza",
      situacao: "Ativo" as const,
      inicioAcompanhamento: "2026-03-02",
      necessidadesEducacionais: null,
      observacoesAcademicas: null,
      resumo: null,
      versao: "v1",
    };
    const cliente = new QueryClient();
    const arvore = (a: typeof aluno) => (
      <QueryClientProvider client={cliente}>
        <AlunoForm aluno={a} aoConcluir={vi.fn()} />
      </QueryClientProvider>
    );
    const { rerender } = render(arvore(aluno));
    await screen.findByRole("option", { name: "Ana Souza" });
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "Salvar" }));
    await waitFor(() => expect(alunoService.atualizar).toHaveBeenCalledTimes(1));

    rerender(arvore({ ...aluno, versao: "v2" }));
    await user.click(screen.getByRole("button", { name: "Salvar" }));
    await waitFor(() => expect(alunoService.atualizar).toHaveBeenCalledTimes(2));
    expect(vi.mocked(alunoService.atualizar).mock.calls[1]?.[2]).toBe("v1");
  });
});
