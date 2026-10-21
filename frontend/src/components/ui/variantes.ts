export type VarianteSemantica = "urgente" | "aguardando" | "info" | "atividade" | "realizado" | "neutro";

export const classesSemanticas: Record<VarianteSemantica, string> = {
  urgente: "bg-urgente-fundo text-urgente",
  aguardando: "bg-aguardando-fundo text-aguardando-texto",
  info: "bg-info-fundo text-info",
  atividade: "bg-atividade-fundo text-atividade",
  realizado: "bg-realizado-fundo text-realizado",
  neutro: "bg-hachura text-suave",
};
