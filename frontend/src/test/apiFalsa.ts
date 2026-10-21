import type { AxiosAdapter } from "axios";
import { api } from "../lib/api";

/** Responde por URL, sem rede, as consultas que chamam `api` direto (catálogos, painel). */
export function responderApi(respostas: Record<string, unknown>): void {
  const adapter: AxiosAdapter = async (config) => {
    const url = config.url ?? "";
    if (!(url in respostas)) throw new Error(`Sem resposta falsa para ${url}`);
    return { status: 200, statusText: "", data: respostas[url], headers: {}, config };
  };
  api.defaults.adapter = adapter;
}
