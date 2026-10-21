import type { AtendimentoAvulsoAgenda, Compromisso } from "../../../lib/types";

export type EstiloCompromisso = {
  variante: "info" | "realizado" | "urgente" | "atividade" | "neutro";
  padrao: "solido" | "hachurado" | "tracejado";
  rotuloEstado: string;
};

export const ROTULO_TIPO: Record<Compromisso["tipo"], string> = {
  Atendimento: "Atendimento",
  ReuniaoEquipe: "Reunião de equipe",
  ReuniaoCoordenacaoGestao: "Reunião com coordenação ou gestão",
  ElaboracaoPei: "Elaboração de PEI",
  TarefaAdministrativa: "Tarefa administrativa",
  EncaminhamentoDevolutiva: "Encaminhamento ou devolutiva",
  AtividadeCurso: "Atividade no curso",
  Bloqueio: "Bloqueio",
  Lembrete: "Lembrete",
};

export const ROTULO_TIPO_ATENDIMENTO: Record<AtendimentoAvulsoAgenda["tipo"], string> = {
  AcolhimentoInicial: "Acolhimento inicial",
  Psicopedagogico: "Psicopedagógico",
  Acompanhamento: "Acompanhamento",
  Devolutiva: "Devolutiva",
  Reuniao: "Reunião",
  OrientacaoAcademica: "Orientação acadêmica",
};

type EstadoAtendimento = AtendimentoAvulsoAgenda["estado"];

/** Estilo de um atendimento já registrado (vinculado a compromisso ou avulso). */
export function estiloAtendimento(estado: EstadoAtendimento): EstiloCompromisso {
  if (estado === "Realizado") return { variante: "realizado", padrao: "solido", rotuloEstado: "Realizado" };
  if (estado === "Ausente") return { variante: "urgente", padrao: "hachurado", rotuloEstado: "Ausente" };
  return { variante: "neutro", padrao: "solido", rotuloEstado: estado };
}

export function estiloCompromisso(c: Compromisso, agora: Date = new Date()): EstiloCompromisso {
  // A listagem semanal omite Cancelado/Remarcado; o mapeamento cobre os dois por completude (texto riscado no bloco).
  if (c.estado === "Cancelado" || c.estado === "Remarcado") return { variante: "neutro", padrao: "solido", rotuloEstado: c.estado };
  if (c.estado === "Concluido") return c.estadoAtendimento ? estiloAtendimento(c.estadoAtendimento) : { variante: "realizado", padrao: "solido", rotuloEstado: "Concluído" };
  if (c.tipo === "Bloqueio") return { variante: "neutro", padrao: "tracejado", rotuloEstado: "Agendado" };
  if (c.tipo === "Lembrete") return { variante: "neutro", padrao: "solido", rotuloEstado: "Agendado" };
  if (c.tipo !== "Atendimento") return { variante: "atividade", padrao: "solido", rotuloEstado: "Agendado" };
  if (new Date(c.fim) < agora) return { variante: "neutro", padrao: "solido", rotuloEstado: "Sem desfecho" };
  return { variante: "info", padrao: "solido", rotuloEstado: "Agendado" };
}
