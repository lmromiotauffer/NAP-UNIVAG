// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { AtividadeAvulsaAgenda, Compromisso, EscalaSemana } from "../../../lib/types";
import { GradeSemanal } from "./GradeSemanal";

const DIAS = ["2026-09-28", "2026-09-29", "2026-09-30", "2026-10-01", "2026-10-02", "2026-10-03", "2026-10-04"];
const escala: EscalaSemana = {
  inicio: "2026-09-28",
  dias: DIAS.map((data, i) => ({
    data,
    inicio: i < 5 ? "07:00:00" : null,
    fim: i < 5 ? "17:00:00" : null,
    semAtendimento: i === 4,
    feriado: null,
  })),
  fuso: "America/Cuiaba",
};

function compromisso(id: string, inicio: string, fim: string, extra: Partial<Compromisso> = {}): Compromisso {
  return {
    id,
    tipo: "Atendimento",
    titulo: "Atendimento",
    inicio,
    fim,
    profissionalId: "p1",
    profissional: "Ana Souza",
    alunoId: "a1",
    aluno: "Letícia Ramos",
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

function montar(compromissos: Compromisso[], equipe?: { id: string; nome: string }[], escalaDaSemana = escala, atividades: AtividadeAvulsaAgenda[] = []) {
  render(
    <GradeSemanal
      escala={escalaDaSemana}
      compromissos={compromissos}
      avulsos={[]}
      atividades={atividades}
      agora={new Date("2026-09-28T09:30:00Z")}
      aoAbrirCompromisso={vi.fn()}
      aoAbrirAvulso={vi.fn()}
      equipe={equipe}
    />,
  );
}

describe("GradeSemanal", () => {
  it("consecutivos_nao_se_sobrepoem_na_grade", () => {
    montar([
      compromisso("c1", "2026-09-29T07:00:00-04:00", "2026-09-29T08:00:00-04:00", { aluno: "Primeiro Aluno" }),
      compromisso("c2", "2026-09-29T08:00:00-04:00", "2026-09-29T09:00:00-04:00", { aluno: "Segundo Aluno" }),
    ]);
    const primeiro = screen.getByRole("button", { name: /Primeiro Aluno/ });
    const segundo = screen.getByRole("button", { name: /Segundo Aluno/ });
    const fimDoPrimeiro = parseFloat(primeiro.style.top) + parseFloat(primeiro.style.height);
    expect(parseFloat(primeiro.style.top)).toBe(0);
    expect(parseFloat(segundo.style.top)).toBeCloseTo(fimDoPrimeiro);
  });

  it("bloco_tem_nome_acessivel_completo", () => {
    montar([
      compromisso("c1", "2026-09-28T10:00:00-04:00", "2026-09-28T11:00:00-04:00", {
        estado: "Concluido",
        estadoAtendimento: "Ausente",
      }),
    ]);
    expect(screen.getByRole("button", { name: "Letícia Ramos, 10h00–11h00, Atendimento, ausente" })).toBeInTheDocument();
  });

  it("fora_do_expediente_nao_vira_botao_de_altura_zero", () => {
    montar([
      compromisso("c1", "2026-09-29T06:00:00-04:00", "2026-09-29T06:30:00-04:00", { aluno: "Antes Aluno" }),
      compromisso("c2", "2026-09-29T17:00:00-04:00", "2026-09-29T18:00:00-04:00", { aluno: "Depois Aluno" }),
    ]);
    for (const nome of [/Antes Aluno/, /Depois Aluno/]) {
      const bloco = screen.getByRole("button", { name: nome });
      expect(parseFloat(bloco.style.height)).toBeGreaterThan(0);
      expect(parseFloat(bloco.style.top) + parseFloat(bloco.style.height)).toBeLessThanOrEqual(100);
    }
  });

  it("item_que_termina_depois_do_fechamento_alarga_a_grade", () => {
    montar([compromisso("c1", "2026-09-29T16:30:00-04:00", "2026-09-29T18:00:00-04:00", { aluno: "Extra Aluno" })]);
    const bloco = screen.getByRole("button", { name: /Extra Aluno/ });
    expect(parseFloat(bloco.style.top) + parseFloat(bloco.style.height)).toBeCloseTo(100);
  });

  it("escala_que_abre_as_07h30_rotula_so_horas_cheias", () => {
    montar([], undefined, { ...escala, dias: escala.dias.map((d) => (d.inicio ? { ...d, inicio: "07:30:00" } : d)) });
    expect(screen.queryByText("7.5h")).not.toBeInTheDocument();
    expect(screen.getByText("08h")).toBeInTheDocument();
    expect(screen.getByText("16h")).toBeInTheDocument();
    expect(screen.queryByText("07h")).not.toBeInTheDocument();
  });

  it("sabado_sem_faixa_aparece_quando_ha_item_nesse_dia", () => {
    expect(screen.queryByRole("group", { name: /^Sábado/ })).not.toBeInTheDocument();
    montar([compromisso("c1", "2026-10-03T09:00:00-04:00", "2026-10-03T10:00:00-04:00", { aluno: "Sabado Aluno" })], [{ id: "p1", nome: "Ana Souza" }]);
    expect(screen.getByRole("group", { name: /^Sábado, 03\/10\/2026/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Sabado Aluno/ })).toBeInTheDocument();
  });

  it("toda_a_equipe_tem_cabecalho_de_sub_colunas_com_nome_completo", () => {
    montar([], [
      { id: "p1", nome: "Ana Souza" },
      { id: "p2", nome: "Bruno Lima" },
    ]);
    expect(screen.getAllByText("AS", { selector: "[aria-hidden=true]" })).toHaveLength(5);
    expect(screen.getAllByText("Bruno Lima", { selector: ".sr-only" })).toHaveLength(5);
  });

  it("atividade_avulsa_aparece_como_bloco_somente_leitura_com_nome_acessivel", async () => {
    const aoAbrirAtividade = vi.fn();
    render(
      <GradeSemanal
        escala={escala}
        compromissos={[]}
        avulsos={[]}
        atividades={[{ id: "at1", inicio: "2026-09-29T08:00:00-04:00", fim: "2026-09-29T09:30:00-04:00", profissionalId: "p1", profissional: "Ana Souza", tipo: "RodaConversa" }]}
        agora={new Date("2026-09-28T09:30:00Z")}
        aoAbrirCompromisso={vi.fn()}
        aoAbrirAvulso={vi.fn()}
        aoAbrirAtividade={aoAbrirAtividade}
      />,
    );
    const bloco = screen.getByRole("button", { name: "Roda de conversa, 8h00–9h30, Atividade no curso, atividade avulsa" });
    // Posições em % da grade (07h–17h = 600 min): 08h00 começa em 60 min e dura 90 min.
    expect(parseFloat(bloco.style.top)).toBeCloseTo((60 / 600) * 100);
    expect(parseFloat(bloco.style.height)).toBeCloseTo((90 / 600) * 100);
    bloco.click();
    expect(aoAbrirAtividade).toHaveBeenCalledOnce();
  });
});
