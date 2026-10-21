import { api, comChave, comVersao } from "../../../lib/api";
import type { components, paths } from "../../../lib/api-schema";
import type { AgendaSemana, EscalaSemana, Json } from "../../../lib/types";

export type CriarCompromissoBody = Json<paths["/api/agenda"]["post"]["requestBody"]>;
export type AtualizarCompromissoBody = Json<paths["/api/agenda/{id}"]["put"]["requestBody"]>;
export type RemarcarCompromissoBody = Json<paths["/api/agenda/{id}/remarcar"]["post"]["requestBody"]>;
export type CancelarCompromissoBody = Json<paths["/api/agenda/{id}/cancelar"]["post"]["requestBody"]>;
/** Compromisso de grupo: `Minha` só cancela a própria instância; `Grupo` vale para todas as agendadas (criador ou coordenação). */
export type EscopoGrupo = components["schemas"]["EscopoGrupo"];

export const agendaService = {
  semana: (inicio: string, profissionalId?: string) =>
    api.get<AgendaSemana>("/agenda/semana", { params: { inicio, profissionalId } }).then((r) => r.data),
  escalaSemana: (semana: string, profissionalId?: string) =>
    api.get<EscalaSemana>("/agenda/escala-semana", { params: { semana, profissionalId } }).then((r) => r.data),
  criar: (body: CriarCompromissoBody, chave: string) =>
    api.post<{ id: string }>("/agenda", body, comChave(chave)).then((r) => r.data),
  atualizar: (id: string, body: AtualizarCompromissoBody, versao: string, escopo?: EscopoGrupo) =>
    api.put(`/agenda/${id}`, body, { ...comVersao(versao), params: { escopo } }).then(() => undefined),
  remarcar: (id: string, body: RemarcarCompromissoBody, chave: string, versao: string, escopo?: EscopoGrupo) =>
    api
      .post<{ id: string }>(`/agenda/${id}/remarcar`, body, { headers: { ...comChave(chave).headers, ...comVersao(versao).headers }, params: { escopo } })
      .then((r) => r.data),
  cancelar: (id: string, body: CancelarCompromissoBody, versao: string, escopo?: EscopoGrupo) =>
    api.post(`/agenda/${id}/cancelar`, body, { ...comVersao(versao), params: { escopo } }).then(() => undefined),
};
