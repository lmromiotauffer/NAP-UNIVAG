import { api, comChave, comVersao } from "../../../lib/api";
import type { paths } from "../../../lib/api-schema";
import { baixarArquivo } from "../../../lib/baixarArquivo";
import type { Documento, Json, PagedResult } from "../../../lib/types";

export type FiltroDocumentos = NonNullable<paths["/api/alunos/{alunoId}/documentos"]["get"]["parameters"]["query"]>;
export type AlterarCategoriaBody = Json<paths["/api/documentos/{id}/categoria"]["put"]["requestBody"]>;
export type CompartilharBody = Json<paths["/api/documentos/{id}/compartilhar"]["post"]["requestBody"]>;
export type ArquivarBody = Json<paths["/api/documentos/{id}/arquivar"]["post"]["requestBody"]>;

export const documentoService = {
  listar: (alunoId: string, f: FiltroDocumentos) => api.get<PagedResult<Documento>>(`/alunos/${alunoId}/documentos`, { params: f }).then((r) => r.data),
  anexar: (alunoId: string, form: FormData, chave: string) =>
    api.post<{ id: string }>(`/alunos/${alunoId}/documentos`, form, comChave(chave)).then((r) => r.data),
  // Arquivado só baixa para a coordenação e com incluirArquivados=true.
  baixar: (d: Pick<Documento, "id" | "arquivado">) => baixarArquivo(`/documentos/${d.id}/arquivo${d.arquivado ? "?incluirArquivados=true" : ""}`),
  alterarCategoria: (id: string, body: AlterarCategoriaBody, versao: string) => api.put(`/documentos/${id}/categoria`, body, comVersao(versao)).then(() => undefined),
  compartilhar: (id: string, body: CompartilharBody, versao: string) => api.post(`/documentos/${id}/compartilhar`, body, comVersao(versao)).then(() => undefined),
  revogar: (id: string, body: CompartilharBody, versao: string) => api.post(`/documentos/${id}/revogar-compartilhamento`, body, comVersao(versao)).then(() => undefined),
  arquivar: (id: string, body: ArquivarBody, versao: string) => api.post(`/documentos/${id}/arquivar`, body, comVersao(versao)).then(() => undefined),
};
