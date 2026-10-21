import { keepPreviousData, useMutation, useQuery } from "@tanstack/react-query";
import { queryKeys, useInvalidar } from "../../../lib/queryKeys";
import { notificacaoService, type FiltroNotificacoes } from "../services/notificacaoService";

export function useListaNotificacoes(f: FiltroNotificacoes) {
  return useQuery({ queryKey: queryKeys.notificacoes.lista(f), queryFn: () => notificacaoService.listar(f), placeholderData: keepPreviousData });
}

/** Alimenta o selo da navegação: atualiza a cada minuto e ao voltar para a aba. */
export function useContagemNotificacoes(habilitado = true) {
  return useQuery({
    queryKey: queryKeys.notificacoes.contagem,
    queryFn: () => notificacaoService.contagem(),
    enabled: habilitado,
    refetchInterval: 60_000,
    refetchOnWindowFocus: true,
  });
}

export function useMarcarLida() {
  const invalidar = useInvalidar([["notificacoes"]]);
  return useMutation({ mutationFn: (id: string) => notificacaoService.marcarLida(id), onSettled: invalidar });
}

export function useMarcarTodasLidas() {
  const invalidar = useInvalidar([["notificacoes"]]);
  return useMutation({ mutationFn: () => notificacaoService.marcarTodasLidas(), onSettled: invalidar });
}
