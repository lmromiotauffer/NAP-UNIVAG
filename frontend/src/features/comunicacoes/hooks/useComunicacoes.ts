import { keepPreviousData, useMutation, useQuery } from "@tanstack/react-query";
import { INVALIDAR_APOS_MENSAGEM, queryKeys, useInvalidar } from "../../../lib/queryKeys";
import {
  comunicacaoService,
  type EnviarMensagemBody,
  type FiltroCaixa,
  type FiltroDestinatarios,
  type RascunhoBody,
} from "../services/comunicacaoService";

export function useCaixa(f: FiltroCaixa) {
  return useQuery({ queryKey: queryKeys.comunicacoes.caixa(f), queryFn: () => comunicacaoService.caixa(f), placeholderData: keepPreviousData });
}

export function useDetalheMensagem(id: string) {
  return useQuery({ queryKey: queryKeys.comunicacoes.detalhe(id), queryFn: () => comunicacaoService.obter(id) });
}

/** Mesmo ritmo do contador de notificações: os dois somam o selo da navegação. */
export function useContagemMensagens(habilitado = true) {
  return useQuery({
    queryKey: queryKeys.comunicacoes.contagem,
    queryFn: () => comunicacaoService.contagem(),
    enabled: habilitado,
    refetchInterval: 60_000,
    refetchOnWindowFocus: true,
  });
}

export function useDestinatarios(f: FiltroDestinatarios) {
  return useQuery({ queryKey: queryKeys.comunicacoes.destinatarios(f), queryFn: () => comunicacaoService.destinatarios(f) });
}

export function useCriarRascunho() {
  const invalidar = useInvalidar(INVALIDAR_APOS_MENSAGEM);
  return useMutation({ mutationFn: ({ body, chave }: { body: RascunhoBody; chave: string }) => comunicacaoService.criarRascunho(body, chave), onSettled: invalidar });
}

export function useAtualizarRascunho() {
  const invalidar = useInvalidar(INVALIDAR_APOS_MENSAGEM);
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: RascunhoBody; versao: string }) => comunicacaoService.atualizarRascunho(id, body, versao),
    onSettled: invalidar,
  });
}

export function useDescartarRascunho() {
  const invalidar = useInvalidar(INVALIDAR_APOS_MENSAGEM);
  return useMutation({ mutationFn: (id: string) => comunicacaoService.descartarRascunho(id), onSettled: invalidar });
}

export function useEnviarMensagem() {
  const invalidar = useInvalidar(INVALIDAR_APOS_MENSAGEM);
  return useMutation({
    mutationFn: ({ body, chave, versao }: { body: EnviarMensagemBody; chave: string; versao?: string }) => comunicacaoService.enviar(body, chave, versao),
    onSettled: invalidar,
  });
}

export function useResponderMensagem() {
  const invalidar = useInvalidar(INVALIDAR_APOS_MENSAGEM);
  return useMutation({
    mutationFn: ({ id, corpo, chave }: { id: string; corpo: string; chave: string }) => comunicacaoService.responder(id, { corpo }, chave),
    onSettled: invalidar,
  });
}

export function useMarcarMensagemLida() {
  const invalidar = useInvalidar(INVALIDAR_APOS_MENSAGEM);
  return useMutation({ mutationFn: (id: string) => comunicacaoService.marcarLida(id), onSettled: invalidar });
}

export function useArquivarMensagem() {
  const invalidar = useInvalidar(INVALIDAR_APOS_MENSAGEM);
  return useMutation({
    mutationFn: ({ id, arquivar }: { id: string; arquivar: boolean }) => (arquivar ? comunicacaoService.arquivar(id) : comunicacaoService.desarquivar(id)),
    onSettled: invalidar,
  });
}
