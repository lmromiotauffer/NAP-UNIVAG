import { api, comChave, comVersao } from "../../../lib/api";
import type { components, paths } from "../../../lib/api-schema";
import { baixarArquivo } from "../../../lib/baixarArquivo";
import type { Json, PeiResumo, VersaoPeiDetalhe } from "../../../lib/types";

export type CriarPeiBody = Json<paths["/api/alunos/{alunoId}/peis"]["post"]["requestBody"]>;
export type ConteudoPeiBody = components["schemas"]["ConteudoPeiInput"];
export type TrocarResponsavelBody = Json<paths["/api/peis/{id}/responsavel"]["post"]["requestBody"]>;
type AcessoGerado = components["schemas"]["AcessoGeradoDto"];

const v = (id: string) => `/peis/versoes/${id}`;
const semCorpo = (p: Promise<unknown>) => p.then(() => undefined);

export const peiService = {
  listarDoAluno: (alunoId: string) => api.get<PeiResumo[]>(`/alunos/${alunoId}/peis`).then((r) => r.data),
  obter: (id: string) => api.get<VersaoPeiDetalhe>(v(id)).then((r) => r.data),
  criar: (alunoId: string, body: CriarPeiBody, chave: string) =>
    api.post<{ id: string }>(`/alunos/${alunoId}/peis`, body, comChave(chave)).then((r) => r.data),
  novaVersao: (peiId: string, chave: string) => api.post<{ id: string }>(`/peis/${peiId}/versoes`, undefined, comChave(chave)).then((r) => r.data),
  salvarConteudo: (id: string, body: ConteudoPeiBody, versao: string) => semCorpo(api.put(`${v(id)}/conteudo`, body, comVersao(versao))),
  enviarRevisao: (id: string, versao: string) => semCorpo(api.post(`${v(id)}/enviar-revisao`, undefined, comVersao(versao))),
  aprovar: (id: string, versao: string) => semCorpo(api.post(`${v(id)}/aprovar`, undefined, comVersao(versao))),
  devolver: (id: string, justificativa: string, versao: string) => semCorpo(api.post(`${v(id)}/devolver`, { justificativa }, comVersao(versao))),
  assinar: (id: string, versao: string) => semCorpo(api.post(`${v(id)}/assinar`, undefined, comVersao(versao))),
  publicar: (id: string, versao: string) => semCorpo(api.post(`${v(id)}/publicar`, undefined, comVersao(versao))),
  arquivar: (id: string, motivo: string, versao: string) => semCorpo(api.post(`${v(id)}/arquivar`, { motivo }, comVersao(versao))),
  trocarResponsavel: (peiId: string, body: TrocarResponsavelBody) => semCorpo(api.post(`/peis/${peiId}/responsavel`, body)),
  ciencia: (id: string) => semCorpo(api.post(`${v(id)}/ciencia`)),
  descartar: (id: string) => semCorpo(api.delete(v(id))),
  // Token e código só existem nesta resposta: quem chama não deve guardá-la em cache.
  gerarAcesso: (id: string) => api.post<AcessoGerado>(`${v(id)}/acesso-aluno`).then((r) => r.data),
  baixarPdf: (id: string) => baixarArquivo(`${v(id)}/pdf`),
};
