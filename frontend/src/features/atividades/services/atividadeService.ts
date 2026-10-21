import { api, comChave, comVersao } from "../../../lib/api";
import type { paths } from "../../../lib/api-schema";
import type { AtividadeDetalhe, AtividadeLista, Json, PagedResult } from "../../../lib/types";

export type FiltroAtividades = NonNullable<paths["/api/atividades"]["get"]["parameters"]["query"]>;
export type RegistrarAtividadeBody = Json<paths["/api/atividades"]["post"]["requestBody"]>;
export type CorrigirAtividadeBody = Json<paths["/api/atividades/{id}"]["put"]["requestBody"]>;

export const atividadeService = {
  listar: (f: FiltroAtividades) => api.get<PagedResult<AtividadeLista>>("/atividades", { params: f }).then((r) => r.data),
  obter: (id: string) => api.get<AtividadeDetalhe>(`/atividades/${id}`).then((r) => r.data),
  registrar: (body: RegistrarAtividadeBody, chave: string) =>
    api.post<{ id: string }>("/atividades", body, comChave(chave)).then((r) => r.data),
  // A API devolve o detalhe atualizado; a tela só precisa saber que deu certo (as consultas são invalidadas).
  corrigir: (id: string, body: CorrigirAtividadeBody, versao: string) => api.put(`/atividades/${id}`, body, comVersao(versao)).then(() => undefined),
};
