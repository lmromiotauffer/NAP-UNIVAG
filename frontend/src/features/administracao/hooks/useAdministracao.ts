import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { queryKeys, useInvalidar } from "../../../lib/queryKeys";
import {
  administracaoService,
  type AtualizarUsuarioBody,
  type AlertasBody,
  type CriarAutorizacaoBody,
  type CriarEscalaBody,
  type CriarFeriadoBody,
  type CriarUsuarioBody,
  type CursoBody,
  type FiltroAuditoria,
  type FiltroAutorizacoes,
  type FiltroUsuariosBody,
  type SituacaoBody,
  type VigenciaEscalaBody,
} from "../services/administracaoService";

export function useAuditoria(f: FiltroAuditoria) {
  return useQuery({ queryKey: queryKeys.administracao.auditoria(f), queryFn: () => administracaoService.listarAuditoria(f), placeholderData: keepPreviousData });
}

export function useListaAutorizacoes(f: FiltroAutorizacoes, habilitado = true) {
  return useQuery({ queryKey: queryKeys.administracao.autorizacoes(f), queryFn: () => administracaoService.listarAutorizacoes(f), placeholderData: keepPreviousData, enabled: habilitado });
}

function useInvalidarAutorizacoes() {
  return useInvalidar([["administracao", "autorizacoes"]]);
}

export function useCriarAutorizacao() {
  const invalidar = useInvalidarAutorizacoes();
  return useMutation({ mutationFn: ({ body, chave }: { body: CriarAutorizacaoBody; chave: string }) => administracaoService.criarAutorizacao(body, chave), onSuccess: invalidar });
}

export function useRevogarAutorizacao() {
  const invalidar = useInvalidarAutorizacoes();
  return useMutation({ mutationFn: (id: string) => administracaoService.revogarAutorizacao(id), onSettled: invalidar });
}

export function useListaUsuarios(f: FiltroUsuariosBody) {
  return useQuery({ queryKey: queryKeys.administracao.usuarios(f), queryFn: () => administracaoService.listarUsuarios(f), placeholderData: keepPreviousData });
}

// Usuário aparece em listas de profissionais, destinatários, escalas, agenda, atendimentos, painel e produção (nome do profissional).
function useInvalidarUsuarios() {
  return useInvalidar([["administracao", "usuarios"], ["administracao", "escalas"], queryKeys.profissionais, ["comunicacoes", "destinatarios"], ["agenda"], ["atendimentos"], ["painel"], ["producao"]]);
}

/** `gcTime: 0` + `reset()` ao fechar o diálogo: a senha não fica no cache de mutações. */
export function useCriarUsuario() {
  const invalidar = useInvalidarUsuarios();
  return useMutation({ mutationFn: (body: CriarUsuarioBody) => administracaoService.criarUsuario(body), gcTime: 0, onSettled: invalidar });
}

export function useGerarSenhaTemporaria() {
  const invalidar = useInvalidarUsuarios();
  return useMutation({ mutationFn: (id: string) => administracaoService.gerarSenhaTemporaria(id), gcTime: 0, onSettled: invalidar });
}

export function useAtualizarUsuario() {
  const invalidar = useInvalidarUsuarios();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: AtualizarUsuarioBody; versao: string }) => administracaoService.atualizarUsuario(id, body, versao),
    onSettled: invalidar,
  });
}

export function useAlterarSituacaoUsuario() {
  const invalidar = useInvalidarUsuarios();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: SituacaoBody; versao: string }) => administracaoService.situacaoUsuario(id, body, versao),
    onSettled: invalidar,
  });
}

// O nome do curso aparece na lista de usuários (coordenador de curso) e nas telas de alunos.
function useInvalidarCursos() {
  return useInvalidar([queryKeys.cursos, ["alunos"], ["administracao", "usuarios"]]);
}

export function useCriarCurso() {
  const invalidar = useInvalidarCursos();
  return useMutation({ mutationFn: ({ body, chave }: { body: CursoBody; chave: string }) => administracaoService.criarCurso(body, chave), onSuccess: invalidar });
}

export function useRenomearCurso() {
  const invalidar = useInvalidarCursos();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: CursoBody; versao: string }) => administracaoService.renomearCurso(id, body, versao),
    onSettled: invalidar,
  });
}

export function useAlterarSituacaoCurso() {
  const invalidar = useInvalidarCursos();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: SituacaoBody; versao: string }) => administracaoService.situacaoCurso(id, body, versao),
    onSettled: invalidar,
  });
}

export function useListaEscalas(profissionalId?: string) {
  return useQuery({ queryKey: queryKeys.administracao.escalas(profissionalId), queryFn: () => administracaoService.listarEscalas(profissionalId), placeholderData: keepPreviousData });
}

// A grade da agenda lê a escala semanal: qualquer mudança de escala ou feriado a invalida.
function useInvalidarEscalas() {
  return useInvalidar([["administracao", "escalas"], ["agenda"]]);
}

export function useCriarEscala() {
  const invalidar = useInvalidarEscalas();
  return useMutation({ mutationFn: ({ body, chave }: { body: CriarEscalaBody; chave: string }) => administracaoService.criarEscala(body, chave), onSettled: invalidar });
}

export function useEncerrarVigenciaEscala() {
  const invalidar = useInvalidarEscalas();
  return useMutation({
    mutationFn: ({ id, body, versao }: { id: string; body: VigenciaEscalaBody; versao: string }) => administracaoService.vigenciaEscala(id, body, versao),
    onSettled: invalidar,
  });
}

export function useListaFeriados(ano: number) {
  return useQuery({ queryKey: queryKeys.administracao.feriados(ano), queryFn: () => administracaoService.listarFeriados(ano), placeholderData: keepPreviousData });
}

function useInvalidarFeriados() {
  return useInvalidar([["administracao", "feriados"], ["agenda"]]);
}

export function useCriarFeriado() {
  const invalidar = useInvalidarFeriados();
  return useMutation({ mutationFn: (body: CriarFeriadoBody) => administracaoService.criarFeriado(body), onSettled: invalidar });
}

export function useInativarFeriado() {
  const invalidar = useInvalidarFeriados();
  return useMutation({ mutationFn: (id: string) => administracaoService.inativarFeriado(id), onSettled: invalidar });
}

export function useAlertasConfig() {
  return useQuery({ queryKey: queryKeys.administracao.alertas, queryFn: administracaoService.obterAlertas });
}

export function useSalvarAlertas() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ body, versao }: { body: AlertasBody; versao: string }) => administracaoService.salvarAlertas(body, versao),
    // Salvar muda os limites usados pelo painel, pelos alertas e pelo prazo dos encaminhamentos.
    onSettled: () => Promise.all([queryKeys.administracao.alertas, ["painel"], ["encaminhamentos"]].map((queryKey) => qc.invalidateQueries({ queryKey }))),
  });
}
