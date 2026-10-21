import type { components } from "../../lib/api-schema";

type Categoria = components["schemas"]["CategoriaProducao"];

/** Categorias que aceitam ajuste: a ociosidade é derivada da capacidade e não se ajusta. */
export const ROTULO_CATEGORIA_AJUSTE: Record<Exclude<Categoria, "Ociosidade">, string> = {
  AtendimentosRealizados: "Atendimentos realizados",
  Ausencias: "Ausências de alunos",
  ReunioesEquipe: "Reuniões de equipe",
  ReunioesCoordenacao: "Reuniões com coordenação, diretoria ou gestão",
  ElaboracaoPei: "Elaboração de PEI",
  Administrativo: "Encaminhamentos, agenda, relatórios e tarefas administrativas",
  AtividadesCursos: "Atividades nos cursos",
};
