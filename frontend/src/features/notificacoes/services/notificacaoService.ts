import { api } from "../../../lib/api";
import type { paths } from "../../../lib/api-schema";
import type { Notificacao, PagedResult } from "../../../lib/types";

export type FiltroNotificacoes = NonNullable<paths["/api/notificacoes"]["get"]["parameters"]["query"]>;

export const notificacaoService = {
  listar: (f: FiltroNotificacoes) => api.get<PagedResult<Notificacao>>("/notificacoes", { params: f }).then((r) => r.data),
  contagem: () => api.get<{ naoLidas: number }>("/notificacoes/contagem").then((r) => r.data),
  marcarLida: (id: string) => api.post(`/notificacoes/${id}/lida`).then(() => undefined),
  marcarTodasLidas: () => api.post("/notificacoes/lidas").then(() => undefined),
};
