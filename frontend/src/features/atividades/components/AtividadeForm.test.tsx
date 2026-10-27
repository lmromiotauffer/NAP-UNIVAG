// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { Compromisso } from "../../../lib/types";
import { responderApi } from "../../../test/apiFalsa";
import { atividadeService } from "../services/atividadeService";
import { AtividadeForm } from "./AtividadeForm";

vi.mock("../services/atividadeService", () => ({ atividadeService: { registrar: vi.fn() } }));

const registrar = vi.mocked(atividadeService.registrar);

const compromisso: Compromisso = {
  id: "c1",
  tipo: "AtividadeCurso",
  titulo: "Roda de conversa",
  inicio: "2026-09-29T08:00:00-04:00",
  fim: "2026-09-29T09:30:00-04:00",
  profissionalId: "p1",
  profissional: "Ana Souza",
  alunoId: null,
  aluno: null,
  estado: "Agendado",
  estadoAtendimento: null,
  remarcadoDeId: null,
  descricao: null,
  versao: "v1",
  grupoId: null,
  participantes: null,
  podeGerirGrupo: false,
};

function montar(c?: Compromisso) {
  responderApi({
    "/cursos": [
      { id: "cu1", nome: "Direito", ativo: true, versao: "v" },
      { id: "cu2", nome: "Curso extinto", ativo: false, versao: "v" },
    ],
  });
  const aoConcluir = vi.fn();
  render(
    <QueryClientProvider client={new QueryClient()}>
      <AtividadeForm compromisso={c} aoConcluir={aoConcluir} />
    </QueryClientProvider>,
  );
  return { aoConcluir, user: userEvent.setup() };
}

beforeEach(() => {
  registrar.mockReset();
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("AtividadeForm", () => {
  it("Registrar_a_partir_do_compromisso_preenche_o_horario", async () => {
    registrar.mockResolvedValueOnce({ id: "at1" });
    const { user, aoConcluir } = montar(compromisso);
    expect(screen.getByLabelText(/Data/)).toHaveValue("2026-09-29");
    expect(screen.getByLabelText(/Início/)).toHaveValue("08:00");
    expect(screen.getByLabelText(/Fim/)).toHaveValue("09:30");
    expect(screen.queryByLabelText(/Justificativa/)).not.toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText(/Tipo/), "RodaConversa");
    await user.type(screen.getByLabelText(/Descrição/), "Acolhimento dos calouros");
    await user.click(screen.getByRole("button", { name: "Registrar" }));

    await waitFor(() => expect(aoConcluir).toHaveBeenCalledWith("at1"));
    const [corpo, chave] = registrar.mock.calls[0] ?? [];
    expect(corpo).toEqual({
      compromissoId: "c1",
      profissionalId: null,
      inicio: "2026-09-29T08:00:00-04:00",
      fim: "2026-09-29T09:30:00-04:00",
      cursoId: null,
      tipo: "RodaConversa",
      descricao: "Acolhimento dos calouros",
      justificativaAvulso: null,
    });
    expect(chave).toMatch(/^[0-9a-f-]{36}$/);
  });

  it("Avulsa_exige_justificativa", async () => {
    const { user } = montar();
    await user.type(screen.getByLabelText(/Data/), "2026-09-29");
    await user.type(screen.getByLabelText(/Início/), "08:00");
    await user.type(screen.getByLabelText(/Fim/), "09:00");
    await user.selectOptions(screen.getByLabelText(/Tipo/), "Outra");
    await user.type(screen.getByLabelText(/Descrição/), "Visita à turma");
    await user.click(screen.getByRole("button", { name: "Registrar" }));

    expect(await screen.findByText("Informe a justificativa (de 5 a 500 caracteres).")).toBeInTheDocument();
    expect(screen.getByLabelText(/Justificativa/)).toHaveAttribute("aria-invalid", "true");
    expect(registrar).not.toHaveBeenCalled();
  });

  it("curso_so_lista_ativos_e_oferece_institucional", async () => {
    montar();
    expect(await screen.findByRole("option", { name: "Direito" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Institucional (sem curso)" })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "Curso extinto" })).not.toBeInTheDocument();
  });

  it("conflito_409_aparece_no_alerta_e_mantem_o_formulario", async () => {
    registrar.mockRejectedValueOnce({ status: 409, titulo: "Conflito", detalhe: "Este agendamento já possui atividade registrada.", errosCampo: {} });
    const { user, aoConcluir } = montar(compromisso);
    await user.selectOptions(screen.getByLabelText(/Tipo/), "Outra");
    await user.type(screen.getByLabelText(/Descrição/), "Reunião com a turma");
    await user.click(screen.getByRole("button", { name: "Registrar" }));

    expect(await screen.findByText("Este agendamento já possui atividade registrada.")).toBeInTheDocument();
    expect(aoConcluir).not.toHaveBeenCalled();
    expect(screen.getByLabelText(/Descrição/)).toHaveValue("Reunião com a turma");
  });
  it("Termino_no_futuro_e_descricao_curta_sao_recusados", async () => {
    // "Agora" fixo às 08:30 do dia da atividade: o término às 09:00 ainda não chegou (restaurado no afterEach).
    vi.spyOn(Date, "now").mockReturnValue(Date.parse("2026-09-29T08:30:00-04:00"));
    const { user } = montar();
    // Digitar num input[type=date] vazio não registrou o valor no jsdom; ele entra por evento de mudança.
    fireEvent.change(screen.getByLabelText(/Data/), { target: { value: "2026-09-29" } });
    await user.type(screen.getByLabelText(/Início/), "08:00");
    await user.type(screen.getByLabelText(/Fim/), "09:00");
    await user.selectOptions(screen.getByLabelText(/Tipo/), "Outra");
    await user.type(screen.getByLabelText(/Descrição/), "ab");
    await user.click(screen.getByRole("button", { name: "Registrar" }));

    expect(await screen.findByText("A atividade ainda não terminou; registre depois do horário de término.")).toBeInTheDocument();
    expect(screen.getByText("Descreva a atividade (de 3 a 2000 caracteres).")).toBeInTheDocument();
    expect(registrar).not.toHaveBeenCalled();
  });
});
