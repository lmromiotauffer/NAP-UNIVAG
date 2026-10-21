import { api, comChave, comVersao } from "../../../lib/api";
import type { paths } from "../../../lib/api-schema";
import type { Destinatario, Json, MensagemDetalhe, MensagemLista, PagedResult } from "../../../lib/types";

export type FiltroCaixa = NonNullable<paths["/api/comunicacoes"]["get"]["parameters"]["query"]>;
export type FiltroDestinatarios = NonNullable<paths["/api/comunicacoes/destinatarios"]["get"]["parameters"]["query"]>;
export type EnviarMensagemBody = Json<paths["/api/comunicacoes"]["post"]["requestBody"]>;
export type RascunhoBody = Json<paths["/api/comunicacoes/rascunhos"]["post"]["requestBody"]>;
type ResponderBody = Json<paths["/api/comunicacoes/{id}/respostas"]["post"]["requestBody"]>;

export const comunicacaoService = {
  caixa: (f: FiltroCaixa) => api.get<PagedResult<MensagemLista>>("/comunicacoes", { params: f }).then((r) => r.data),
  obter: (id: string) => api.get<MensagemDetalhe>(`/comunicacoes/${id}`).then((r) => r.data),
  contagem: () => api.get<{ naoLidas: number }>("/comunicacoes/contagem").then((r) => r.data),
  destinatarios: (f: FiltroDestinatarios) => api.get<Destinatario[]>("/comunicacoes/destinatarios", { params: f }).then((r) => r.data),
  criarRascunho: (body: RascunhoBody, chave: string) => api.post<{ id: string }>("/comunicacoes/rascunhos", body, comChave(chave)).then((r) => r.data),
  atualizarRascunho: (id: string, body: RascunhoBody, versao: string) => api.put(`/comunicacoes/rascunhos/${id}`, body, comVersao(versao)).then(() => undefined),
  descartarRascunho: (id: string) => api.delete(`/comunicacoes/rascunhos/${id}`).then(() => undefined),
  // `versao` (rascunho aberto): a API recusa com 409 um rascunho alterado em outra aba.
  enviar: (body: EnviarMensagemBody, chave: string, versao?: string) =>
    api.post<{ id: string }>("/comunicacoes", body, { headers: { ...comChave(chave).headers, ...(versao ? comVersao(versao).headers : {}) } }).then((r) => r.data),
  responder: (id: string, body: ResponderBody, chave: string) => api.post<{ id: string }>(`/comunicacoes/${id}/respostas`, body, comChave(chave)).then((r) => r.data),
  marcarLida: (id: string) => api.post(`/comunicacoes/${id}/lida`).then(() => undefined),
  arquivar: (id: string) => api.post(`/comunicacoes/${id}/arquivar`).then(() => undefined),
  desarquivar: (id: string) => api.post(`/comunicacoes/${id}/desarquivar`).then(() => undefined),
};
