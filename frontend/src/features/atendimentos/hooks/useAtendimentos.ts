import { keepPreviousData, useMutation, useQuery } from "@tanstack/react-query";
import { INVALIDAR_APOS_ATENDIMENTO, queryKeys, useInvalidar } from "../../../lib/queryKeys";
import { atendimentoService, type CorrigirAtendimentoBody, type FiltroAtendimentos, type RegistrarAtendimentoBody } from "../services/atendimentoService";

export function useListaAtendimentos(f: FiltroAtendimentos) {
  return useQuery({ queryKey: queryKeys.atendimentos.lista(f), queryFn: () => atendimentoService.listar(f), placeholderData: keepPreviousData });
}

export function useDetalheAtendimento(id: string) {
  return useQuery({ queryKey: queryKeys.atendimentos.detalhe(id), queryFn: () => atendimentoService.obter(id) });
}

function useInvalidarAtendimento() {
  return useInvalidar(INVALIDAR_APOS_ATENDIMENTO);
}

export function useRegistrarAtendimento() {
  const invalidar = useInvalidarAtendimento();
  return useMutation({
    mutationFn: ({ body, chave }: { body: RegistrarAtendimentoBody; chave: string }) => atendimentoService.registrar(body, chave),
    onSuccess: invalidar,
  });
}

export function useCorrigirAtendimento() {
  const invalidar = useInvalidarAtendimento();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: CorrigirAtendimentoBody; versao: string }) => atendimentoService.corrigir(id, body, versao),
    onSettled: invalidar,
  });
}
