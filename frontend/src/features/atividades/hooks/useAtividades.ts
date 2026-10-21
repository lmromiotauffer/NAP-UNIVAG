import { keepPreviousData, useMutation, useQuery } from "@tanstack/react-query";
import { INVALIDAR_APOS_ATIVIDADE, queryKeys, useInvalidar } from "../../../lib/queryKeys";
import { atividadeService, type CorrigirAtividadeBody, type FiltroAtividades, type RegistrarAtividadeBody } from "../services/atividadeService";

export function useListaAtividades(f: FiltroAtividades) {
  return useQuery({ queryKey: queryKeys.atividades.lista(f), queryFn: () => atividadeService.listar(f), placeholderData: keepPreviousData });
}

export function useDetalheAtividade(id: string) {
  return useQuery({ queryKey: queryKeys.atividades.detalhe(id), queryFn: () => atividadeService.obter(id) });
}

function useInvalidarAtividade() {
  return useInvalidar(INVALIDAR_APOS_ATIVIDADE);
}

export function useRegistrarAtividade() {
  const invalidar = useInvalidarAtividade();
  return useMutation({
    mutationFn: ({ body, chave }: { body: RegistrarAtividadeBody; chave: string }) => atividadeService.registrar(body, chave),
    onSettled: invalidar,
  });
}

export function useCorrigirAtividade() {
  const invalidar = useInvalidarAtividade();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: CorrigirAtividadeBody; versao: string }) => atividadeService.corrigir(id, body, versao),
    onSettled: invalidar,
  });
}
