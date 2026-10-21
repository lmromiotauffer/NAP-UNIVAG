import { api, comChave, comVersao } from "../../../lib/api";
import { baixarArquivo } from "../../../lib/baixarArquivo";
import type { paths } from "../../../lib/api-schema";
import type { AjusteProducao, Producao, SemanaProducao } from "../../../lib/types";

export type FiltroProducao = NonNullable<paths["/api/producao"]["get"]["parameters"]["query"]>;
export type FormatoExportacao = "pdf" | "xlsx";
export type FiltroSemanas = NonNullable<paths["/api/producao/semanas"]["get"]["parameters"]["query"]>;
export type FiltroAjustes = NonNullable<paths["/api/producao/ajustes"]["get"]["parameters"]["query"]>;
export type RegistrarRealizacaoBody = NonNullable<paths["/api/agenda/{id}/realizacao"]["post"]["requestBody"]>["content"]["application/json"];
export type CorrigirRealizacaoBody = NonNullable<paths["/api/agenda/{id}/realizacao"]["put"]["requestBody"]>["content"]["application/json"];
export type FecharSemanaBody = NonNullable<paths["/api/producao/semanas/fechar"]["post"]["requestBody"]>["content"]["application/json"];
export type ReabrirSemanaBody = NonNullable<paths["/api/producao/semanas/reabrir"]["post"]["requestBody"]>["content"]["application/json"];
export type CriarAjusteBody = NonNullable<paths["/api/producao/ajustes"]["post"]["requestBody"]>["content"]["application/json"];

export const producaoService = {
  consultar: (f: FiltroProducao) => api.get<Producao>("/producao", { params: f }).then((r) => r.data),
  // Mesmos parâmetros da tela: o servidor recorta igual a `consultar`.
  exportar: (formato: FormatoExportacao, f: FiltroProducao) => {
    const q = new URLSearchParams({ formato });
    for (const [k, v] of Object.entries(f)) if (v != null && v !== "") q.set(k, String(v));
    return baixarArquivo(`/producao/exportar?${q}`);
  },
  registrarRealizacao: (id: string, body: RegistrarRealizacaoBody, versao: string) =>
    api.post(`/agenda/${id}/realizacao`, body, comVersao(versao)).then(() => undefined),
  corrigirRealizacao: (id: string, body: CorrigirRealizacaoBody, versao: string) =>
    api.put(`/agenda/${id}/realizacao`, body, comVersao(versao)).then(() => undefined),
  listarSemanas: (f: FiltroSemanas) => api.get<SemanaProducao[]>("/producao/semanas", { params: f }).then((r) => r.data),
  fecharSemana: (body: FecharSemanaBody) => api.post("/producao/semanas/fechar", body).then(() => undefined),
  reabrirSemana: (body: ReabrirSemanaBody, versao: string) => api.post("/producao/semanas/reabrir", body, comVersao(versao)).then(() => undefined),
  listarAjustes: (f: FiltroAjustes) => api.get<AjusteProducao[]>("/producao/ajustes", { params: f }).then((r) => r.data),
  criarAjuste: (body: CriarAjusteBody, chave: string) =>
    api.post<{ id: string }>("/producao/ajustes", body, comChave(chave)).then((r) => r.data),
};
