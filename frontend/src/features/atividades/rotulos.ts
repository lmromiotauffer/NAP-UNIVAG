import type { components } from "../../lib/api-schema";

export type TipoAtividade = components["schemas"]["TipoAtividade"];

export const ROTULO_TIPO_ATIVIDADE: Record<TipoAtividade, string> = {
  RodaConversa: "Roda de conversa",
  ReuniaoPedagogica: "Reunião pedagógica",
  OrientacaoColetiva: "Orientação coletiva",
  IntervencaoTurma: "Intervenção em turma",
  FormacaoDocente: "Formação docente",
  Outra: "Outra",
};
export const TIPOS_ATIVIDADE = Object.keys(ROTULO_TIPO_ATIVIDADE) as [TipoAtividade, ...TipoAtividade[]];
export const ehTipoAtividade = (t: string): t is TipoAtividade => (TIPOS_ATIVIDADE as string[]).includes(t);

export const ROTULO_TURNO = { Manha: "Manhã", Tarde: "Tarde", Noite: "Noite" } as const;
