import { describe, expect, it } from "vitest";
import type { Compromisso } from "../../../lib/types";
import { estiloCompromisso } from "./estiloCompromisso";

const AGORA = new Date("2026-10-01T14:00:00Z");

function compromisso(extra: Partial<Compromisso> = {}): Compromisso {
  return {
    id: "c1",
    tipo: "Atendimento",
    titulo: "Atendimento",
    inicio: "2026-10-01T15:00:00-04:00",
    fim: "2026-10-01T16:00:00-04:00",
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

describe("estiloCompromisso", () => {
  it("ausente_e_hachurado_e_tem_rotulo_textual", () => {
    const e = estiloCompromisso(compromisso({ estado: "Concluido", estadoAtendimento: "Ausente" }), AGORA);
    expect(e).toEqual({ variante: "urgente", padrao: "hachurado", rotuloEstado: "Ausente" });
  });

  it("passado_sem_registro_e_sem_desfecho", () => {
    const e = estiloCompromisso(compromisso({ inicio: "2026-09-29T08:00:00-04:00", fim: "2026-09-29T09:00:00-04:00" }), AGORA);
    expect(e).toEqual({ variante: "neutro", padrao: "solido", rotuloEstado: "Sem desfecho" });
  });

  it("demais_estados", () => {
    expect(estiloCompromisso(compromisso(), AGORA)).toEqual({ variante: "info", padrao: "solido", rotuloEstado: "Agendado" });
    expect(estiloCompromisso(compromisso({ estado: "Concluido", estadoAtendimento: "Realizado" }), AGORA)).toMatchObject({ variante: "realizado", rotuloEstado: "Realizado" });
    expect(estiloCompromisso(compromisso({ tipo: "Bloqueio", alunoId: null, aluno: null }), AGORA)).toMatchObject({ variante: "neutro", padrao: "tracejado" });
    expect(estiloCompromisso(compromisso({ tipo: "ReuniaoEquipe", alunoId: null, aluno: null }), AGORA)).toMatchObject({ variante: "atividade", padrao: "solido" });
    expect(estiloCompromisso(compromisso({ estado: "Cancelado" }), AGORA)).toMatchObject({ variante: "neutro", rotuloEstado: "Cancelado" });
    expect(estiloCompromisso(compromisso({ estado: "Remarcado" }), AGORA)).toMatchObject({ variante: "neutro", rotuloEstado: "Remarcado" });
  });
});
