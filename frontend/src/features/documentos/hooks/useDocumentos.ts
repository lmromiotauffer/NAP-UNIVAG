import { keepPreviousData, useMutation, useQuery } from "@tanstack/react-query";
import { INVALIDAR_APOS_DOCUMENTO, queryKeys, useInvalidar } from "../../../lib/queryKeys";
import { documentoService, type AlterarCategoriaBody, type ArquivarBody, type CompartilharBody, type FiltroDocumentos } from "../services/documentoService";

export function useListaDocumentos(alunoId: string, f: FiltroDocumentos) {
  return useQuery({ queryKey: queryKeys.documentos.lista(alunoId, f), queryFn: () => documentoService.listar(alunoId, f), placeholderData: keepPreviousData });
}

function useInvalidarDocumento() {
  return useInvalidar(INVALIDAR_APOS_DOCUMENTO);
}

export function useAnexarDocumento() {
  const invalidar = useInvalidarDocumento();
  return useMutation({
    mutationFn: ({ alunoId, form, chave }: { alunoId: string; form: FormData; chave: string }) => documentoService.anexar(alunoId, form, chave),
    onSettled: invalidar,
  });
}

export function useAlterarCategoria() {
  const invalidar = useInvalidarDocumento();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: AlterarCategoriaBody; versao: string }) => documentoService.alterarCategoria(id, body, versao),
    onSettled: invalidar,
  });
}

export function useCompartilharDocumento(revogar: boolean) {
  const invalidar = useInvalidarDocumento();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: CompartilharBody; versao: string }) =>
      revogar ? documentoService.revogar(id, body, versao) : documentoService.compartilhar(id, body, versao),
    onSettled: invalidar,
  });
}

export function useArquivarDocumento() {
  const invalidar = useInvalidarDocumento();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: ArquivarBody; versao: string }) => documentoService.arquivar(id, body, versao),
    onSettled: invalidar,
  });
}
