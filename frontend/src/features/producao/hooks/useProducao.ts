import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { INVALIDAR_APOS_AGENDA, queryKeys } from "../../../lib/queryKeys";
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
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: RegistrarRealizacaoBody; versao: string }) => producaoService.registrarRealizacao(id, body, versao),
    onSettled: () => Promise.all(INVALIDAR_APOS_AGENDA.map((queryKey) => qc.invalidateQueries({ queryKey }))),
  });
}

export function useCorrigirRealizacao() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: CorrigirRealizacaoBody; versao: string }) => producaoService.corrigirRealizacao(id, body, versao),
    onSettled: () => Promise.all(INVALIDAR_APOS_AGENDA.map((queryKey) => qc.invalidateQueries({ queryKey }))),
  });
}

export function useSemanasProducao(f: FiltroSemanas) {
  return useQuery({ queryKey: queryKeys.producao.semanas(f), queryFn: () => producaoService.listarSemanas(f), placeholderData: keepPreviousData });
}

export function useAjustesProducao(f: FiltroAjustes) {
  return useQuery({ queryKey: queryKeys.producao.ajustes(f), queryFn: () => producaoService.listarAjustes(f), placeholderData: keepPreviousData });
}

function useMutacaoFechamento<V>(fn: (v: V) => Promise<unknown>) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: fn, onSettled: () => Promise.all(INVALIDAR_APOS_FECHAMENTO.map((queryKey) => qc.invalidateQueries({ queryKey }))) });
}

export const useFecharSemana = () => useMutacaoFechamento((body: FecharSemanaBody) => producaoService.fecharSemana(body));
export const useReabrirSemana = () => useMutacaoFechamento(({ body, versao }: { body: ReabrirSemanaBody; versao: string }) => producaoService.reabrirSemana(body, versao));
export const useCriarAjuste = () => useMutacaoFechamento(({ body, chave }: { body: CriarAjusteBody; chave: string }) => producaoService.criarAjuste(body, chave));
