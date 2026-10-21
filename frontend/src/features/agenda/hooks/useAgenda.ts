import { keepPreviousData, useMutation, useQuery } from "@tanstack/react-query";
import { INVALIDAR_APOS_AGENDA, queryKeys, useInvalidar } from "../../../lib/queryKeys";
import {
  agendaService,
  type AtualizarCompromissoBody,
  type CancelarCompromissoBody,
  type CriarCompromissoBody,
  type EscopoGrupo,
  type RemarcarCompromissoBody,
} from "../services/agendaService";

export function useSemana(inicio: string, profissionalId?: string) {
  return useQuery({
    queryKey: queryKeys.agenda.semana(inicio, profissionalId),
    queryFn: () => agendaService.semana(inicio, profissionalId),
    placeholderData: keepPreviousData,
  });
}

export function useEscalaSemana(semana: string, profissionalId?: string) {
  return useQuery({
    queryKey: queryKeys.agenda.escalaSemana(semana, profissionalId),
    queryFn: () => agendaService.escalaSemana(semana, profissionalId),
    placeholderData: keepPreviousData,
  });
}

function useInvalidarAgenda() {
  return useInvalidar(INVALIDAR_APOS_AGENDA);
}

export function useCriarCompromisso() {
  const invalidar = useInvalidarAgenda();
  return useMutation({
    mutationFn: ({ body, chave }: { body: CriarCompromissoBody; chave: string }) => agendaService.criar(body, chave),
    onSuccess: invalidar,
  });
}

export function useAtualizarCompromisso() {
  const invalidar = useInvalidarAgenda();
  return useMutation({
    mutationFn: ({ id, body, versao, escopo }: { id: string; body: AtualizarCompromissoBody; versao: string; escopo?: EscopoGrupo }) =>
      agendaService.atualizar(id, body, versao, escopo),
    onSettled: invalidar,
  });
}

export function useRemarcarCompromisso() {
  const invalidar = useInvalidarAgenda();
  return useMutation({
    mutationFn: ({ id, body, chave, versao, escopo }: { id: string; body: RemarcarCompromissoBody; chave: string; versao: string; escopo?: EscopoGrupo }) =>
      agendaService.remarcar(id, body, chave, versao, escopo),
    onSettled: invalidar,
  });
}

export function useCancelarCompromisso() {
  const invalidar = useInvalidarAgenda();
  return useMutation({
    mutationFn: ({ id, body, versao, escopo }: { id: string; body: CancelarCompromissoBody; versao: string; escopo?: EscopoGrupo }) =>
      agendaService.cancelar(id, body, versao, escopo),
    onSettled: invalidar,
  });
}
