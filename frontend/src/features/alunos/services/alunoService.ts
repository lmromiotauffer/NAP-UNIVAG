import { api, comChave, comVersao } from "../../../lib/api";
import type { paths } from "../../../lib/api-schema";
import type { AlunoFicha, AlunoLista, EventoLinhaDoTempo, Json, PagedResult } from "../../../lib/types";

export type FiltroAlunosBody = NonNullable<paths["/api/alunos"]["get"]["parameters"]["query"]>;
export type CriarAlunoBody = Json<paths["/api/alunos"]["post"]["requestBody"]>;
export type AtualizarAlunoBody = Json<paths["/api/alunos/{id}"]["put"]["requestBody"]>;
export type AlterarSituacaoBody = Json<paths["/api/alunos/{id}/situacao"]["post"]["requestBody"]>;
export type TransferirCursoBody = Json<paths["/api/alunos/{id}/transferencia"]["post"]["requestBody"]>;

// Mesmo limite das outras listas da ficha: cabe na tela abaixo do cabeçalho e dos cartões.
const TAMANHO_LINHA_DO_TEMPO = 10;

export const alunoService = {
  listar: (f: FiltroAlunosBody) => api.get<PagedResult<AlunoLista>>("/alunos", { params: f }).then((r) => r.data),
  obter: (id: string) => api.get<AlunoFicha>(`/alunos/${id}`).then((r) => r.data),
  linhaDoTempo: (id: string, pagina: number) =>
    api
      .get<PagedResult<EventoLinhaDoTempo>>(`/alunos/${id}/linha-do-tempo`, { params: { pagina, tamanho: TAMANHO_LINHA_DO_TEMPO } })
      .then((r) => r.data),
  criar: (body: CriarAlunoBody, chave: string) =>
    api.post<{ id: string }>("/alunos", body, comChave(chave)).then((r) => r.data),
  atualizar: (id: string, body: AtualizarAlunoBody, versao: string) => api.put(`/alunos/${id}`, body, comVersao(versao)).then(() => undefined),
  alterarSituacao: (id: string, body: AlterarSituacaoBody, versao: string) => api.post(`/alunos/${id}/situacao`, body, comVersao(versao)).then(() => undefined),
  transferir: (id: string, body: TransferirCursoBody, versao: string) => api.post(`/alunos/${id}/transferencia`, body, comVersao(versao)).then(() => undefined),
};
