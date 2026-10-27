import { useMutation, useQuery } from "@tanstack/react-query";
import { INVALIDAR_APOS_PEI, queryKeys, useInvalidar } from "../../../lib/queryKeys";
import { peiService, type ConteudoPeiBody, type CriarPeiBody, type TrocarResponsavelBody } from "../services/peiService";

export function useListaPeis(alunoId: string) {
  return useQuery({ queryKey: queryKeys.peis.doAluno(alunoId), queryFn: () => peiService.listarDoAluno(alunoId) });
}

export function useVersaoPei(id: string) {
  return useQuery({ queryKey: queryKeys.peis.versao(id), queryFn: () => peiService.obter(id) });
}

function useInvalidarPei() {
  return useInvalidar(INVALIDAR_APOS_PEI);
}

export function useCriarPei() {
  const invalidar = useInvalidarPei();
  return useMutation({
    mutationFn: ({ alunoId, body, chave }: { alunoId: string; body: CriarPeiBody; chave: string }) => peiService.criar(alunoId, body, chave),
    onSettled: invalidar,
  });
}

export function useNovaVersaoPei() {
  const invalidar = useInvalidarPei();
  return useMutation({ mutationFn: ({ peiId, chave }: { peiId: string; chave: string }) => peiService.novaVersao(peiId, chave), onSettled: invalidar });
}

export function useSalvarConteudo() {
  const invalidar = useInvalidarPei();
  return useMutation({ mutationFn: ({ id, body, versao }: { id: string; body: ConteudoPeiBody; versao: string }) => peiService.salvarConteudo(id, body, versao), onSettled: invalidar });
}

export function useTrocarResponsavelPei() {
  const invalidar = useInvalidarPei();
  return useMutation({ mutationFn: ({ peiId, body }: { peiId: string; body: TrocarResponsavelBody }) => peiService.trocarResponsavel(peiId, body), onSettled: invalidar });
}

export type AcaoVersao = "enviarRevisao" | "aprovar" | "devolver" | "assinar" | "publicar" | "arquivar" | "ciencia" | "descartar";

/** Ações de transição de uma versão; `texto` é a justificativa (devolver) ou o motivo (arquivar). */
export function useAcaoVersao() {
  const invalidar = useInvalidarPei();
  return useMutation({
    mutationFn: ({ acao, id, versao, texto = "" }: { acao: AcaoVersao; id: string; versao: string; texto?: string }) => {
      switch (acao) {
        case "devolver":
          return peiService.devolver(id, texto, versao);
        case "arquivar":
          return peiService.arquivar(id, texto, versao);
        case "ciencia":
          return peiService.ciencia(id);
        case "descartar":
          return peiService.descartar(id, versao);
        default:
          return peiService[acao](id, versao);
      }
    },
    onSettled: invalidar,
  });
}

/** useMutation (não useQuery): token e código não podem ficar no cache; `reset()` ao fechar o diálogo. */
export function useGerarAcessoAluno() {
  const invalidar = useInvalidarPei();
  return useMutation({ mutationFn: (id: string) => peiService.gerarAcesso(id), gcTime: 0, onSettled: invalidar });
}
