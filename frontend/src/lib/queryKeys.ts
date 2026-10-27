import { type QueryKey, useQueryClient } from "@tanstack/react-query";

type Filtro = Record<string, unknown>;

/** Remove "" (undefined já some no hash da chave); a ordem das chaves não importa no hash do TanStack. */
const normalizar = (f: Filtro = {}): Filtro => Object.fromEntries(Object.entries(f).filter(([, v]) => v !== ""));

export const queryKeys = {
  cursos: ["cursos"] as const,
  profissionais: ["profissionais"] as const,
  alunos: {
    lista: (f?: Filtro) => ["alunos", "lista", normalizar(f)] as const,
    ficha: (id: string) => ["alunos", "ficha", id] as const,
    linhaDoTempo: (id: string, p?: Filtro) => ["alunos", "linha-do-tempo", id, normalizar(p)] as const,
  },
  agenda: {
    semana: (inicio: string, profissionalId?: string) => ["agenda", "semana", inicio, profissionalId ?? null] as const,
    escalaSemana: (semana: string, profissionalId?: string) => ["agenda", "escala-semana", semana, profissionalId ?? null] as const,
  },
  atendimentos: {
    lista: (f?: Filtro) => ["atendimentos", "lista", normalizar(f)] as const,
    detalhe: (id: string) => ["atendimentos", "detalhe", id] as const,
  },
  atividades: {
    lista: (f?: Filtro) => ["atividades", "lista", normalizar(f)] as const,
    detalhe: (id: string) => ["atividades", "detalhe", id] as const,
  },
  encaminhamentos: {
    lista: (f?: Filtro) => ["encaminhamentos", "lista", normalizar(f)] as const,
    detalhe: (id: string) => ["encaminhamentos", "detalhe", id] as const,
  },
  comunicacoes: {
    caixa: (f?: Filtro) => ["comunicacoes", "caixa", normalizar(f)] as const,
    detalhe: (id: string) => ["comunicacoes", "detalhe", id] as const,
    contagem: ["comunicacoes", "contagem"] as const,
    destinatarios: (f?: Filtro) => ["comunicacoes", "destinatarios", normalizar(f)] as const,
  },
  notificacoes: {
    lista: (f?: Filtro) => ["notificacoes", "lista", normalizar(f)] as const,
    contagem: ["notificacoes", "contagem"] as const,
  },
  documentos: {
    lista: (alunoId: string, f?: Filtro) => ["documentos", "lista", alunoId, normalizar(f)] as const,
  },
  peis: {
    doAluno: (alunoId: string) => ["peis", "aluno", alunoId] as const,
    versao: (id: string) => ["peis", "versao", id] as const,
  },
  administracao: {
    usuarios: (f?: Filtro) => ["administracao", "usuarios", normalizar(f)] as const,
    escalas: (profissionalId?: string) => ["administracao", "escalas", profissionalId ?? null] as const,
    autorizacoes: (f?: Filtro) => ["administracao", "autorizacoes", normalizar(f)] as const,
    feriados: (ano: number) => ["administracao", "feriados", ano] as const,
    alertas: ["administracao", "alertas"] as const,
    auditoria: (f?: Filtro) => ["administracao", "auditoria", normalizar(f)] as const,
  },
  producao: {
    consulta: (f?: Filtro) => ["producao", "consulta", normalizar(f)] as const,
    semanas: (f?: Filtro) => ["producao", "semanas", normalizar(f)] as const,
    ajustes: (f?: Filtro) => ["producao", "ajustes", normalizar(f)] as const,
  },
  painel: {
    filtros: (f?: Filtro) => ["painel", "filtros", normalizar(f)] as const,
  },
};

export const INVALIDAR_APOS_ATENDIMENTO = [["atendimentos"], ["alunos"], ["agenda"], ["painel"], ["producao"]] as const;
// Registrar conclui o compromisso da agenda e entra no painel.
export const INVALIDAR_APOS_ATIVIDADE = [["atividades"], ["agenda"], ["painel"], ["producao"]] as const;
export const INVALIDAR_APOS_ENCAMINHAMENTO = [["encaminhamentos"], ["alunos"], ["painel"], ["notificacoes"]] as const;
// Enviar gera notificação para o destinatário; ler uma mensagem muda o contador somado da navegação.
export const INVALIDAR_APOS_MENSAGEM = [["comunicacoes"], ["notificacoes"]] as const;
export const INVALIDAR_APOS_DOCUMENTO = [["documentos"]] as const;
export const INVALIDAR_APOS_AGENDA = [["agenda"], ["painel"], ["alunos"], ["producao"]] as const;
// Agenda, atendimentos, encaminhamentos, comunicações e PEI exibem o nome do aluno: um aluno renomeado precisa atualizar ali também.
export const INVALIDAR_APOS_ALUNO = [["alunos"], ["painel"], ["agenda"], ["atendimentos"], ["encaminhamentos"], ["comunicacoes"], ["peis"]] as const;
export const INVALIDAR_APOS_PEI = [["peis"], ["painel"], ["comunicacoes"], ["notificacoes"]] as const;

/**
 * Devolve uma função que invalida todas as chaves (por prefixo) de uma vez. Não espera os refetches: `mutateAsync` só
 * resolveria depois deles, e o refetch do detalhe de um registro recém-apagado daria 404 na tela antes da navegação.
 */
export function useInvalidar(chaves: readonly QueryKey[]) {
  const qc = useQueryClient();
  return () => {
    void Promise.all(chaves.map((queryKey) => qc.invalidateQueries({ queryKey })));
  };
}

/** Página vinda da URL; ausente, inválida ou 1 vira undefined (a API assume a primeira). */
export function paginaDaUrl(params: URLSearchParams): number | undefined {
  const pagina = Number(params.get("pagina"));
  return Number.isInteger(pagina) && pagina > 1 ? pagina : undefined;
}

/** Define (ou remove, com valor vazio) um parâmetro da URL; mudar um filtro volta à primeira página. */
export function comParametro(atual: URLSearchParams, chave: string, valor: string | null): URLSearchParams {
  const proximo = new URLSearchParams(atual);
  if (valor) proximo.set(chave, valor);
  else proximo.delete(chave);
  if (chave !== "pagina" && chave !== "id") proximo.delete("pagina");
  return proximo;
}

export const temFiltroAtivo = (params: URLSearchParams, chaves: readonly string[]): boolean => chaves.some((c) => params.get(c)?.trim());
