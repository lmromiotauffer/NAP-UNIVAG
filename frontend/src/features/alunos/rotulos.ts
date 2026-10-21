import type { VarianteSemantica } from "../../components/ui/variantes";

export const SITUACOES = ["Ativo", "EmAcompanhamento", "Alta", "Inativo"] as const;
export type Situacao = (typeof SITUACOES)[number];

export const ROTULO_SITUACAO: Record<Situacao, string> = {
  Ativo: "Ativo",
  EmAcompanhamento: "Em acompanhamento",
  Alta: "Alta",
  Inativo: "Inativo",
};

// Azul = em acompanhamento/ativo no NAP; verde = alta; cinza = inativo (legenda de cores do shell).
export const VARIANTE_SITUACAO: Record<Situacao, VarianteSemantica> = {
  Ativo: "info",
  EmAcompanhamento: "info",
  Alta: "realizado",
  Inativo: "neutro",
};
