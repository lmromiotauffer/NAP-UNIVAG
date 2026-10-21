import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api } from "../../../lib/api";
import type { paths } from "../../../lib/api-schema";
import { queryKeys } from "../../../lib/queryKeys";
import type { Painel } from "../../../lib/types";

export type FiltroPainel = NonNullable<paths["/api/painel"]["get"]["parameters"]["query"]>;

export function usePainel(f: FiltroPainel) {
  return useQuery({
    queryKey: queryKeys.painel.filtros(f),
    queryFn: () => api.get<Painel>("/painel", { params: f }).then((r) => r.data),
    placeholderData: keepPreviousData,
  });
}
