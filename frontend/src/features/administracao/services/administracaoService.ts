import { api, comChave, comVersao } from "../../../lib/api";
import type { paths } from "../../../lib/api-schema";
import type { components } from "../../../lib/api-schema";
import type { AutorizacaoForaEscala, ConfiguracaoAlertas, EventoAuditoria, EscalaProfissional, Feriado, Json, PagedResult, SenhaTemporaria, UsuarioAdmin } from "../../../lib/types";

export type FiltroUsuariosBody = NonNullable<paths["/api/usuarios"]["get"]["parameters"]["query"]>;
export type FiltroAuditoria = NonNullable<paths["/api/auditoria"]["get"]["parameters"]["query"]>;
export type CriarUsuarioBody = Json<paths["/api/usuarios"]["post"]["requestBody"]>;
export type AtualizarUsuarioBody = Json<paths["/api/usuarios/{id}"]["put"]["requestBody"]>;
export type SituacaoBody = Json<paths["/api/usuarios/{id}/situacao"]["post"]["requestBody"]>;
export type CursoBody = Json<paths["/api/cursos"]["post"]["requestBody"]>;
export type CriarEscalaBody = Json<paths["/api/escalas"]["post"]["requestBody"]>;
export type VigenciaEscalaBody = Json<paths["/api/escalas/{id}/vigencia"]["put"]["requestBody"]>;
export type CriarFeriadoBody = Json<paths["/api/feriados"]["post"]["requestBody"]>;
export type FeriadoCriado = components["schemas"]["FeriadoCriadoDto"];
export type FiltroAutorizacoes = NonNullable<paths["/api/escalas/autorizacoes"]["get"]["parameters"]["query"]>;
export type CriarAutorizacaoBody = Json<paths["/api/escalas/autorizacoes"]["post"]["requestBody"]>;
export type AlertasBody = Json<paths["/api/configuracoes/alertas"]["put"]["requestBody"]>;

export const administracaoService = {
  listarUsuarios: (f: FiltroUsuariosBody) => api.get<PagedResult<UsuarioAdmin>>("/usuarios", { params: f }).then((r) => r.data),
  // Sem Idempotency-Key: a resposta carrega a senha temporária e não pode ser reapresentada.
  criarUsuario: (body: CriarUsuarioBody) => api.post<SenhaTemporaria>("/usuarios", body).then((r) => r.data),
  atualizarUsuario: (id: string, body: AtualizarUsuarioBody, versao: string) => api.put(`/usuarios/${id}`, body, comVersao(versao)).then(() => undefined),
  situacaoUsuario: (id: string, body: SituacaoBody, versao: string) => api.post(`/usuarios/${id}/situacao`, body, comVersao(versao)).then(() => undefined),
  gerarSenhaTemporaria: (id: string) => api.post<SenhaTemporaria>(`/usuarios/${id}/senha-temporaria`).then((r) => r.data),
  criarCurso: (body: CursoBody, chave: string) => api.post<{ id: string }>("/cursos", body, comChave(chave)).then((r) => r.data),
  renomearCurso: (id: string, body: CursoBody, versao: string) => api.put(`/cursos/${id}`, body, comVersao(versao)).then(() => undefined),
  situacaoCurso: (id: string, body: SituacaoBody, versao: string) => api.post(`/cursos/${id}/situacao`, body, comVersao(versao)).then(() => undefined),
  listarEscalas: (profissionalId?: string) => api.get<EscalaProfissional[]>("/escalas", { params: { profissionalId } }).then((r) => r.data),
  criarEscala: (body: CriarEscalaBody, chave: string) => api.post<{ id: string }>("/escalas", body, comChave(chave)).then((r) => r.data),
  vigenciaEscala: (id: string, body: VigenciaEscalaBody, versao: string) => api.put(`/escalas/${id}/vigencia`, body, comVersao(versao)).then(() => undefined),
  listarAutorizacoes: (f: FiltroAutorizacoes) => api.get<PagedResult<AutorizacaoForaEscala>>("/escalas/autorizacoes", { params: f }).then((r) => r.data),
  criarAutorizacao: (body: CriarAutorizacaoBody, chave: string) => api.post<{ id: string }>("/escalas/autorizacoes", body, comChave(chave)).then((r) => r.data),
  revogarAutorizacao: (id: string) => api.post(`/escalas/autorizacoes/${id}/revogar`).then(() => undefined),
  listarFeriados: (ano: number) => api.get<Feriado[]>("/feriados", { params: { ano } }).then((r) => r.data),
  // Sem Idempotency-Key: a data é única entre ativos, então repetir a chamada vira 409 em vez de duplicar.
  criarFeriado: (body: CriarFeriadoBody) => api.post<FeriadoCriado>("/feriados", body).then((r) => r.data),
  inativarFeriado: (id: string) => api.post(`/feriados/${id}/inativar`).then(() => undefined),
  obterAlertas: () => api.get<ConfiguracaoAlertas>("/configuracoes/alertas").then((r) => r.data),
  listarAuditoria: (f: FiltroAuditoria) => api.get<PagedResult<EventoAuditoria>>("/auditoria", { params: f }).then((r) => r.data),
  salvarAlertas: (body: AlertasBody, versao: string) => api.put<ConfiguracaoAlertas>("/configuracoes/alertas", body, comVersao(versao)).then((r) => r.data),
};
