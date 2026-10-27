// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../../../components/ui/Toast";
import type { Compromisso } from "../../../lib/types";
import { agendaService } from "../services/agendaService";
import { DetalheCompromisso } from "./DetalheCompromisso";

vi.mock("../services/agendaService", () => ({ agendaService: { atualizar: vi.fn(), remarcar: vi.fn(), cancelar: vi.fn() } }));

function compromisso(id: string, extra: Partial<Compromisso> = {}): Compromisso {
  return {
    id,
    tipo: "ReuniaoEquipe",
    titulo: "Reunião semanal",
    inicio: "2026-09-29T08:00:00-04:00",
    fim: "2026-09-29T09:00:00-04:00",
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
    ...extra,
  };
}

function montar(c: Compromisso, aoRegistrarAtendimento?: (c: Compromisso) => void, aoRegistrarAtividade?: (c: Compromisso) => void, propria = true) {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ToastProvider>
        <MemoryRouter>
          <DetalheCompromisso compromisso={c} podeAgir propria={propria} aoFechar={vi.fn()} aoRemarcado={vi.fn()} aoRegistrarAtendimento={aoRegistrarAtendimento} aoRegistrarAtividade={aoRegistrarAtividade} />
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
  return { user: userEvent.setup() };
}

describe("DetalheCompromisso", () => {
  it("registrar_atendimento_so_aparece_depois_do_termino", () => {
    const atendimento = { tipo: "Atendimento" as const, alunoId: "a1", aluno: "Maria" };
    const futuro = new Date(Date.now() + 3_600_000).toISOString();
    montar(compromisso("c1", { ...atendimento, inicio: new Date().toISOString(), fim: futuro }), vi.fn());
    expect(screen.queryByRole("button", { name: "Registrar atendimento" })).not.toBeInTheDocument();
  });

  it("registrar_atendimento_aparece_para_atendimento_ja_terminado", async () => {
    const aoRegistrar = vi.fn();
    const c = compromisso("c1", { tipo: "Atendimento", alunoId: "a1", aluno: "Maria" });
    const { user } = montar(c, aoRegistrar);
    await user.click(screen.getByRole("button", { name: "Registrar atendimento" }));
    expect(aoRegistrar).toHaveBeenCalledWith(c);
  });

  it("registrar_atividade_aparece_para_atividade_no_curso_agendada_e_ja_terminada", async () => {
    const aoRegistrar = vi.fn();
    const c = compromisso("c1", { tipo: "AtividadeCurso" });
    const { user } = montar(c, undefined, aoRegistrar);
    expect(screen.queryByRole("button", { name: "Registrar atendimento" })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Registrar atividade" }));
    expect(aoRegistrar).toHaveBeenCalledWith(c);
  });

  it("registrar_atividade_nao_aparece_sem_permissao_nem_fora_do_estado_agendado_nem_antes_do_termino", () => {
    montar(compromisso("c1", { tipo: "AtividadeCurso" }));
    expect(screen.queryByRole("button", { name: "Registrar atividade" })).not.toBeInTheDocument();
    cleanup();
    montar(compromisso("c2", { tipo: "AtividadeCurso", estado: "Concluido" }), undefined, vi.fn());
    expect(screen.queryByRole("button", { name: "Registrar atividade" })).not.toBeInTheDocument();
    cleanup();
    montar(compromisso("c3", { tipo: "AtividadeCurso", inicio: new Date().toISOString(), fim: new Date(Date.now() + 3_600_000).toISOString() }), undefined, vi.fn());
    expect(screen.queryByRole("button", { name: "Registrar atividade" })).not.toBeInTheDocument();
    cleanup();
    montar(compromisso("c4", { tipo: "ReuniaoEquipe" }), undefined, vi.fn());
    expect(screen.queryByRole("button", { name: "Registrar atividade" })).not.toBeInTheDocument();
  });

  it("remarcado_de_e_texto_neutro", () => {
    montar(compromisso("c1", { remarcadoDeId: "c0" }));
    expect(screen.getByText("Remarcado de um compromisso anterior")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Remarcado/ })).not.toBeInTheDocument();
  });

  it("editar_preenche_a_descricao_atual", async () => {
    const { user } = montar(compromisso("c1", { descricao: "Pauta: PEI" }));
    await user.click(screen.getByRole("button", { name: "Editar" }));
    expect(screen.getByLabelText("Descrição")).toHaveValue("Pauta: PEI");
  });

  it("trocar_de_modo_leva_o_foco_ao_primeiro_campo", async () => {
    const c = compromisso("c1");
    const { user } = montar(c);
    await user.click(screen.getByRole("button", { name: "Editar" }));
    expect(screen.getByLabelText(/^Título/)).toHaveFocus();
    await user.click(screen.getByRole("button", { name: "Voltar" }));
    await user.click(screen.getByRole("button", { name: "Remarcar" }));
    expect(screen.getByLabelText(/^Nova data/)).toHaveFocus();
    await user.click(screen.getByRole("button", { name: "Voltar" }));
    await user.click(screen.getByRole("button", { name: "Não realizado" }));
    expect(screen.getByLabelText(/^Motivo do cancelamento/)).toHaveFocus();
  });

  it("trocar_de_modo_anima_o_painel_mas_a_abertura_e_a_volta_do_cancelamento_nao", async () => {
    const { user } = montar(compromisso("c1"));
    expect(document.querySelector(".animate-surgir")).toBeNull();
    await user.click(screen.getByRole("button", { name: "Remarcar" }));
    expect(document.querySelector(".animate-surgir")).not.toBeNull();
    await user.click(screen.getByRole("button", { name: "Voltar" }));
    expect(document.querySelector(".animate-surgir")).not.toBeNull();
    // O cancelamento é outro diálogo: ao voltar, o diálogo do detalhe monta de novo e já anima por conta própria.
    await user.click(screen.getByRole("button", { name: "Não realizado" }));
    await user.click(screen.getByRole("button", { name: "Voltar" }));
    expect(screen.getByRole("button", { name: "Remarcar" })).toBeInTheDocument();
    expect(document.querySelector(".animate-surgir")).toBeNull();
  });

  it("Realizacao_so_aparece_depois_do_termino", () => {
    const futuro = { inicio: new Date().toISOString(), fim: new Date(Date.now() + 3_600_000).toISOString() };
    montar(compromisso("c1", futuro));
    expect(screen.queryByRole("button", { name: "Registrar realização" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Não realizado" })).not.toBeInTheDocument();
    cleanup();
    montar(compromisso("c2", { tipo: "Atendimento", alunoId: "a1", aluno: "Maria" }));
    expect(screen.queryByRole("button", { name: "Registrar realização" })).not.toBeInTheDocument();
    cleanup();
    montar(compromisso("c3", { estado: "Concluido" }));
    expect(screen.queryByRole("button", { name: "Registrar realização" })).not.toBeInTheDocument();
    cleanup();
    montar(compromisso("c4", { tipo: "TarefaAdministrativa" }));
    expect(screen.getByRole("button", { name: "Registrar realização" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Não realizado" })).toBeInTheDocument();
  });

  it("Registrar_realizacao_abre_o_dialogo_com_o_horario_agendado_e_nao_realizado_pede_o_motivo", async () => {
    const { user } = montar(compromisso("c1"));
    await user.click(screen.getByRole("button", { name: "Registrar realização" }));
    expect(screen.getByLabelText(/^Início/)).toHaveValue("08:00");
    await user.click(screen.getByRole("button", { name: "Voltar" }));
    await user.click(screen.getByRole("button", { name: "Não realizado" }));
    expect(screen.getByLabelText(/^Motivo do cancelamento/)).toHaveFocus();
  });

  it("Corrigir_realizacao_aparece_so_para_compromisso_realizado_e_abre_com_o_horario_efetivo", async () => {
    montar(compromisso("c1", { tipo: "AtividadeCurso", estado: "Concluido" }));
    expect(screen.queryByRole("button", { name: "Corrigir realização" })).not.toBeInTheDocument();
    cleanup();
    const { user } = montar(compromisso("c2", { estado: "Concluido", inicio: "2026-09-29T08:15:00-04:00", fim: "2026-09-29T09:30:00-04:00" }));
    await user.click(screen.getByRole("button", { name: "Corrigir realização" }));
    expect(screen.getByLabelText(/^Início/)).toHaveValue("08:15");
    expect(screen.getByLabelText(/^Fim/)).toHaveValue("09:30");
    expect(screen.getByLabelText(/^Justificativa da correção/)).toBeInTheDocument();
  });

  const grupo = (extra: Partial<Compromisso> = {}) =>
    compromisso("g1", { grupoId: "grupo1", participantes: [{ id: "p1", nome: "Ana Souza" }], ...extra });
  const futuro = { inicio: new Date(Date.now() + 3_600_000).toISOString(), fim: new Date(Date.now() + 7_200_000).toISOString() };

  async function cancelarComMotivo(user: ReturnType<typeof userEvent.setup>) {
    await user.type(screen.getByLabelText(/^Motivo do cancelamento/), "Imprevisto");
    await user.click(screen.getByRole("button", { name: "Confirmar cancelamento" }));
  }

  it("Cancelar_oferece_so_a_minha_ao_participante", async () => {
    vi.mocked(agendaService.cancelar).mockClear();
    const { user } = montar(grupo(futuro));
    expect(screen.queryByRole("button", { name: "Remarcar o grupo" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Editar o grupo" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancelar o grupo" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Remarcar" })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Cancelar minha participação" }));
    await cancelarComMotivo(user);
    await waitFor(() => expect(agendaService.cancelar).toHaveBeenCalledTimes(1));
    expect(vi.mocked(agendaService.cancelar).mock.calls[0]?.[3]).toBe("Minha");
  });

  it("Gestor_cancela_o_grupo_com_escopo_Grupo", async () => {
    vi.mocked(agendaService.cancelar).mockClear();
    const { user } = montar(grupo({ ...futuro, podeGerirGrupo: true }), undefined, undefined, false);
    expect(screen.getByRole("button", { name: "Cancelar só a participação de Ana Souza" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Cancelar o grupo" }));
    await cancelarComMotivo(user);
    await waitFor(() => expect(agendaService.cancelar).toHaveBeenCalledTimes(1));
    expect(vi.mocked(agendaService.cancelar).mock.calls[0]?.[3]).toBe("Grupo");
  });

  it("Instancia_ja_terminada_diz_que_nao_participou", () => {
    montar(grupo());
    expect(screen.getByRole("button", { name: "Não participei (cancelar minha participação)" })).toBeInTheDocument();
  });

  it("Criador_pode_remarcar_o_grupo", async () => {
    vi.mocked(agendaService.remarcar).mockResolvedValue({ id: "novo" });
    const { user } = montar(grupo({ podeGerirGrupo: true }));
    await user.click(screen.getByRole("button", { name: "Remarcar o grupo" }));
    await user.click(screen.getByRole("button", { name: "Remarcar" }));
    await waitFor(() => expect(agendaService.remarcar).toHaveBeenCalledTimes(1));
    expect(vi.mocked(agendaService.remarcar).mock.calls[0]?.[4]).toBe("Grupo");
  });

  it("Criador_edita_o_grupo_com_escopo_Grupo", async () => {
    vi.mocked(agendaService.atualizar).mockResolvedValue(undefined);
    const { user } = montar(grupo({ podeGerirGrupo: true }));
    await user.click(screen.getByRole("button", { name: "Editar o grupo" }));
    await user.click(screen.getByRole("button", { name: "Salvar" }));
    await waitFor(() => expect(agendaService.atualizar).toHaveBeenCalledTimes(1));
    expect(vi.mocked(agendaService.atualizar).mock.calls[0]?.[3]).toBe("Grupo");
  });

  it("Detalhe_lista_os_participantes_do_grupo", () => {
    montar(grupo({ participantes: [{ id: "p1", nome: "Ana Souza" }, { id: "p2", nome: "Bruno Lima" }] }));
    expect(screen.getByText("Ana Souza, Bruno Lima")).toBeInTheDocument();
  });
});
