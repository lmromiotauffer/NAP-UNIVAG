import { keepPreviousData, useMutation, useQuery } from "@tanstack/react-query";
import { INVALIDAR_APOS_ENCAMINHAMENTO, queryKeys, useInvalidar } from "../../../lib/queryKeys";
import {
  encaminhamentoService,
  type CancelarEncaminhamentoBody,
  type CriarEncaminhamentoBody,
  type FiltroEncaminhamentos,
  type RegistrarRetornoBody,
} from "../services/encaminhamentoService";

export function useListaEncaminhamentos(f: FiltroEncaminhamentos) {
  return useQuery({ queryKey: queryKeys.encaminhamentos.lista(f), queryFn: () => encaminhamentoService.listar(f), placeholderData: keepPreviousData });
}

export function useDetalheEncaminhamento(id: string) {
  return useQuery({ queryKey: queryKeys.encaminhamentos.detalhe(id), queryFn: () => encaminhamentoService.obter(id) });
}

function useInvalidarEncaminhamento() {
  return useInvalidar(INVALIDAR_APOS_ENCAMINHAMENTO);
}

export function useCriarEncaminhamento() {
  const invalidar = useInvalidarEncaminhamento();
  return useMutation({
    mutationFn: ({ body, chave }: { body: CriarEncaminhamentoBody; chave: string }) => encaminhamentoService.criar(body, chave),
    onSuccess: invalidar,
  });
}

export function useRegistrarRetorno() {
  const invalidar = useInvalidarEncaminhamento();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: RegistrarRetornoBody; versao: string }) => encaminhamentoService.registrarRetorno(id, body, versao),
    onSettled: invalidar,
  });
}

export function useCancelarEncaminhamento() {
  const invalidar = useInvalidarEncaminhamento();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: CancelarEncaminhamentoBody; versao: string }) => encaminhamentoService.cancelar(id, body, versao),
    onSettled: invalidar,
  });
}
