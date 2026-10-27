import { keepPreviousData, useMutation, useQuery } from "@tanstack/react-query";
import { INVALIDAR_APOS_AGENDA, queryKeys, useInvalidar } from "../../../lib/queryKeys";
import {
  producaoService,
  type CorrigirRealizacaoBody,
  type CriarAjusteBody,
  type FecharSemanaBody,
  type FiltroAjustes,
  type FiltroProducao,
  type FiltroSemanas,
  type ReabrirSemanaBody,
  type RegistrarRealizacaoBody,
} from "../services/producaoService";

// Fechar, reabrir e ajustar mudam semanas, ajustes e produção (["producao"]) e o painel.
const INVALIDAR_APOS_FECHAMENTO = [["producao"], ["painel"]] as const;

export function useProducao(f: FiltroProducao) {
  return useQuery({ queryKey: queryKeys.producao.consulta(f), queryFn: () => producaoService.consultar(f), placeholderData: keepPreviousData });
}

export function useRegistrarRealizacao() {
  const invalidar = useInvalidar(INVALIDAR_APOS_AGENDA);
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: RegistrarRealizacaoBody; versao: string }) => producaoService.registrarRealizacao(id, body, versao),
    onSettled: invalidar,
  });
}

export function useCorrigirRealizacao() {
  const invalidar = useInvalidar(INVALIDAR_APOS_AGENDA);
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: CorrigirRealizacaoBody; versao: string }) => producaoService.corrigirRealizacao(id, body, versao),
    onSettled: invalidar,
  });
}

export function useSemanasProducao(f: FiltroSemanas) {
  return useQuery({ queryKey: queryKeys.producao.semanas(f), queryFn: () => producaoService.listarSemanas(f), placeholderData: keepPreviousData });
}

export function useAjustesProducao(f: FiltroAjustes) {
  return useQuery({ queryKey: queryKeys.producao.ajustes(f), queryFn: () => producaoService.listarAjustes(f), placeholderData: keepPreviousData });
}

function useMutacaoFechamento<V>(fn: (v: V) => Promise<unknown>) {
  const invalidar = useInvalidar(INVALIDAR_APOS_FECHAMENTO);
  return useMutation({ mutationFn: fn, onSettled: invalidar });
}

export const useFecharSemana = () => useMutacaoFechamento((body: FecharSemanaBody) => producaoService.fecharSemana(body));
export const useReabrirSemana = () => useMutacaoFechamento(({ body, versao }: { body: ReabrirSemanaBody; versao: string }) => producaoService.reabrirSemana(body, versao));
export const useCriarAjuste = () => useMutacaoFechamento(({ body, chave }: { body: CriarAjusteBody; chave: string }) => producaoService.criarAjuste(body, chave));
