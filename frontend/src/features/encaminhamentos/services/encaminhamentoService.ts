import { api, comChave, comVersao } from "../../../lib/api";
import type { paths } from "../../../lib/api-schema";
import type { EncaminhamentoDetalhe, EncaminhamentoLista, Json, PagedResult } from "../../../lib/types";

export type FiltroEncaminhamentos = NonNullable<paths["/api/encaminhamentos"]["get"]["parameters"]["query"]>;
export type CriarEncaminhamentoBody = Json<paths["/api/encaminhamentos"]["post"]["requestBody"]>;
export type RegistrarRetornoBody = Json<paths["/api/encaminhamentos/{id}/retornos"]["post"]["requestBody"]>;
export type CancelarEncaminhamentoBody = Json<paths["/api/encaminhamentos/{id}/cancelar"]["post"]["requestBody"]>;

export const encaminhamentoService = {
  listar: (f: FiltroEncaminhamentos) => api.get<PagedResult<EncaminhamentoLista>>("/encaminhamentos", { params: f }).then((r) => r.data),
  // A API audita cada leitura do detalhe (traz o motivo restrito).
  obter: (id: string) => api.get<EncaminhamentoDetalhe>(`/encaminhamentos/${id}`).then((r) => r.data),
  criar: (body: CriarEncaminhamentoBody, chave: string) =>
    api.post<{ id: string }>("/encaminhamentos", body, comChave(chave)).then((r) => r.data),
  registrarRetorno: (id: string, body: RegistrarRetornoBody, versao: string) =>
    api.post(`/encaminhamentos/${id}/retornos`, body, comVersao(versao)).then(() => undefined),
  cancelar: (id: string, body: CancelarEncaminhamentoBody, versao: string) =>
    api.post(`/encaminhamentos/${id}/cancelar`, body, comVersao(versao)).then(() => undefined),
};
