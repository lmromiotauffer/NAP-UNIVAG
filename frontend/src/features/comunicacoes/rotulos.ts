import type { components } from "../../lib/api-schema";

export type TipoMensagem = components["schemas"]["TipoMensagem"];
export type Caixa = components["schemas"]["CaixaMensagens"];

export const ROTULO_TIPO: Record<TipoMensagem, string> = {
  Devolutiva: "Devolutiva",
  Aviso: "Aviso",
  EnvioPei: "Envio de PEI",
  SolicitacaoInformacao: "Solicitação de informação",
  RetornoEncaminhamento: "Retorno de encaminhamento",
};

/** EnvioPei só entra com o PEI (Fatia D); retorno de encaminhamento exige um encaminhamento vinculado. */
export const tiposOferecidos = (comEncaminhamento: boolean): TipoMensagem[] => [
  "Devolutiva",
  "Aviso",
  "SolicitacaoInformacao",
  ...(comEncaminhamento ? (["RetornoEncaminhamento"] as const) : []),
];

export const ROTULO_CAIXA: Record<Caixa, string> = {
  Entrada: "Entrada",
  Enviados: "Enviados",
  Rascunhos: "Rascunhos",
  Arquivadas: "Arquivadas",
};
export const CAIXAS = Object.keys(ROTULO_CAIXA) as Caixa[];

export const AVISO_COORDENACAO = "Não inclua conteúdo clínico em mensagens para a coordenação do curso.";
export const SEM_DESTINATARIOS = "Ninguém pode receber esta comunicação com estes vínculos.";
