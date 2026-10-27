// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { alunoService } from "../services/alunoService";
import { BuscaAluno, type AlunoEscolhido } from "./BuscaAluno";

vi.mock("../services/alunoService", () => ({ alunoService: { listar: vi.fn() } }));

const ana = { id: "a1", nome: "Ana Souza", matricula: "2024001" };
const linha = { ...ana, matriculaAtiva: true, cursoId: "c1", curso: "Direito", semestre: 3, profissionalId: "p1", profissional: "Rafael", situacao: "Ativo" as const, totalAtendimentos: 0, totalFaltas: 0 };

function montar(aluno: AlunoEscolhido | null = null) {
  const aoEscolher = vi.fn();
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <BuscaAluno aluno={aluno} aoEscolher={aoEscolher} obrigatorio />
    </QueryClientProvider>,
  );
  return { user: userEvent.setup(), aoEscolher };
}

describe("BuscaAluno", () => {
  beforeEach(() => {
    vi.mocked(alunoService.listar).mockReset().mockResolvedValue({ itens: [linha], pagina: 1, tamanho: 6, total: 1 });
  });

  it("so_busca_a_partir_de_dois_caracteres_e_uma_vez_apos_o_debounce", async () => {
    const { user, aoEscolher } = montar();
    const campo = screen.getByRole("searchbox", { name: "Aluno" });
    await user.type(campo, "a");
    await new Promise((r) => setTimeout(r, 400));
    expect(alunoService.listar).not.toHaveBeenCalled();

    await user.type(campo, "n");
    await user.click(await screen.findByRole("button", { name: /Ana Souza/ }));
    expect(alunoService.listar).toHaveBeenCalledTimes(1);
    expect(alunoService.listar).toHaveBeenCalledWith({ busca: "an", tamanho: 6 });
    expect(aoEscolher).toHaveBeenCalledWith(ana);
  });

  it("com_aluno_escolhido_nao_busca_e_trocar_aluno_limpa_a_escolha", async () => {
    const { user, aoEscolher } = montar(ana);
    expect(screen.getByText("Ana Souza")).toBeInTheDocument();
    expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Trocar aluno" }));
    expect(aoEscolher).toHaveBeenCalledWith(null);
    expect(alunoService.listar).not.toHaveBeenCalled();
  });
});
