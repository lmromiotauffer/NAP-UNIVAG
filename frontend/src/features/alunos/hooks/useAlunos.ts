import { keepPreviousData, useMutation, useQuery } from "@tanstack/react-query";
import { INVALIDAR_APOS_ALUNO, queryKeys, useInvalidar } from "../../../lib/queryKeys";
import {
  alunoService,
  type AlterarSituacaoBody,
  type AtualizarAlunoBody,
  type CriarAlunoBody,
  type FiltroAlunosBody,
  type TransferirCursoBody,
} from "../services/alunoService";

export function useListaAlunos(f: FiltroAlunosBody) {
  return useQuery({ queryKey: queryKeys.alunos.lista(f), queryFn: () => alunoService.listar(f), placeholderData: keepPreviousData });
}

export function useFichaAluno(id: string) {
  return useQuery({ queryKey: queryKeys.alunos.ficha(id), queryFn: () => alunoService.obter(id) });
}

export function useLinhaDoTempo(id: string, pagina: number) {
  return useQuery({ queryKey: queryKeys.alunos.linhaDoTempo(id, { pagina }), queryFn: () => alunoService.linhaDoTempo(id, pagina) });
}

function useInvalidarAluno() {
  return useInvalidar(INVALIDAR_APOS_ALUNO);
}

export function useCriarAluno() {
  const invalidar = useInvalidarAluno();
  return useMutation({
    mutationFn: ({ body, chave }: { body: CriarAlunoBody; chave: string }) => alunoService.criar(body, chave),
    onSuccess: invalidar,
  });
}

export function useAtualizarAluno() {
  const invalidar = useInvalidarAluno();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: AtualizarAlunoBody; versao: string }) => alunoService.atualizar(id, body, versao),
    onSettled: invalidar,
  });
}

export function useAlterarSituacao() {
  const invalidar = useInvalidarAluno();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: AlterarSituacaoBody; versao: string }) => alunoService.alterarSituacao(id, body, versao),
    onSettled: invalidar,
  });
}

export function useTransferirCurso() {
  const invalidar = useInvalidarAluno();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: TransferirCursoBody; versao: string }) => alunoService.transferir(id, body, versao),
    onSettled: invalidar,
  });
}
