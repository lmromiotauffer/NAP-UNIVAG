import axios from "axios";
import type { components } from "../../../lib/api-schema";

export type VersaoParaAssinatura = components["schemas"]["VersaoParaAssinaturaDto"];

// Instância própria, sem os interceptadores de sessão nem cookies: o fluxo do aluno é anônimo e nunca pode levar o
// Authorization da equipe logada no mesmo navegador (a auditoria registraria o usuário da equipe).
export const apiAssinatura = axios.create({ baseURL: "/api", timeout: 30_000 });

export const assinaturaService = {
  abrir: (token: string, codigo: string) => apiAssinatura.post<VersaoParaAssinatura>("/assinar/abrir", { token, codigo }).then((r) => r.data),
  assinar: (token: string, codigo: string, nomeCompleto: string, aceite: boolean) =>
    apiAssinatura.post("/assinar/assinar", { token, codigo, nomeCompleto, aceite }).then(() => undefined),
};
