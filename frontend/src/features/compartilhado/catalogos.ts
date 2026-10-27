import { useQuery } from "@tanstack/react-query";
import { api } from "../../lib/api";
import { queryKeys } from "../../lib/queryKeys";
import type { Curso, Profissional } from "../../lib/types";

export function useCursos(habilitado = true) {
  return useQuery({ queryKey: queryKeys.cursos, queryFn: () => api.get<Curso[]>("/cursos").then((r) => r.data), staleTime: 5 * 60_000, enabled: habilitado });
}

/** `incluirInativos` só é atendido para coordenação e gestão (filtro da Produção); os demais recebem só os ativos. */
export function useProfissionais(habilitado = true, incluirInativos = false) {
  return useQuery({
    queryKey: incluirInativos ? [...queryKeys.profissionais, "inativos"] : queryKeys.profissionais,
    queryFn: () => api.get<Profissional[]>("/usuarios/profissionais", { params: incluirInativos ? { incluirInativos } : undefined }).then((r) => r.data),
    staleTime: 5 * 60_000,
    enabled: habilitado,
  });
}
