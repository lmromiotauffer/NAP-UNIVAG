import { api, comChave, comVersao } from "../../../lib/api";
import type { paths } from "../../../lib/api-schema";
import type { AtendimentoDetalhe, AtendimentoLista, Json, PagedResult } from "../../../lib/types";

export type FiltroAtendimentos = NonNullable<paths["/api/atendimentos"]["get"]["parameters"]["query"]>;
export type RegistrarAtendimentoBody = Json<paths["/api/atendimentos"]["post"]["requestBody"]>;
export type CorrigirAtendimentoBody = Json<paths["/api/atendimentos/{id}"]["put"]["requestBody"]>;

export const atendimentoService = {
  listar: (f: FiltroAtendimentos) => api.get<PagedResult<AtendimentoLista>>("/atendimentos", { params: f }).then((r) => r.data),
  // A API audita cada leitura do detalhe: só buscar quando o usuário abre o atendimento.
  obter: (id: string) => api.get<AtendimentoDetalhe>(`/atendimentos/${id}`).then((r) => r.data),
  registrar: (body: RegistrarAtendimentoBody, chave: string) =>
    api.post<{ id: string }>("/atendimentos", body, comChave(chave)).then((r) => r.data),
  corrigir: (id: string, body: CorrigirAtendimentoBody, versao: string) => api.put(`/atendimentos/${id}`, body, comVersao(versao)).then(() => undefined),
};
