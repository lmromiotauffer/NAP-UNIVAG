import type { components } from "./api-schema";

type Schemas = components["schemas"];

export type AgendaSemana = Schemas["AgendaSemanaDto"];
export type Alerta = Schemas["AlertaDto"];
export type AlunoFicha = Schemas["AlunoFichaDto"];
export type AlunoLista = Schemas["AlunoListaDto"];
export type AtividadeDetalhe = Schemas["AtividadeDetalheDto"];
export type AtividadeLista = Schemas["AtividadeDto"];
export type AtendimentoAvulsoAgenda = Schemas["AtendimentoAvulsoAgendaDto"];
export type AtividadeAvulsaAgenda = Schemas["AtividadeAvulsaAgendaDto"];
export type AtendimentoDetalhe = Schemas["AtendimentoDetalheDto"];
export type AtendimentoLista = Schemas["AtendimentoListaDto"];
export type Compromisso = Schemas["CompromissoDto"];
export type ConflitoAgenda = Schemas["ConflitoAgendaDto"];
export type Contagem = Schemas["ContagemDto"];
export type Curso = Schemas["CursoDto"];
export type Destinatario = Schemas["DestinatarioDto"];
export type EncaminhamentoDetalhe = Schemas["EncaminhamentoDetalheDto"];
export type EncaminhamentoLista = Schemas["EncaminhamentoListaDto"];
export type Documento = Schemas["DocumentoDto"];
export type DiaEscala = Schemas["DiaEscalaDto"];
export type EscalaProfissional = Schemas["EscalaProfissionalDto"];
export type EscalaSemana = Schemas["EscalaSemanaDto"];
export type EventoLinhaDoTempo = Schemas["EventoLinhaDoTempoDto"];
export type FaixaEscala = Schemas["FaixaEscalaDto"];
export type Feriado = Schemas["FeriadoDto"];
export type ConfiguracaoAlertas = Schemas["ConfiguracaoAlertasDto"];
export type MensagemDetalhe = Schemas["MensagemDetalheDto"];
export type MensagemLista = Schemas["MensagemListaDto"];
export type Notificacao = Schemas["NotificacaoDto"];
export type Painel = Schemas["PainelDto"];
export type Producao = Schemas["ProducaoDto"];
export type CategoriaProducao = Schemas["CategoriaProducaoDto"];
export type TempoProducao = Schemas["TempoDto"];
export type ProducaoProfissional = Schemas["ProducaoProfissionalDto"];
export type ProducaoSemana = Schemas["ProducaoSemanaDto"];
export type SemanaProducao = Schemas["SemanaProducaoDto"];
export type AjusteProducao = Schemas["AjusteProducaoDto"];
export type PeiResumo = Schemas["PeiResumoDto"];
export type Profissional = Schemas["ProfissionalDto"];
export type ResumoAcompanhamento = Schemas["ResumoAcompanhamentoDto"];
export type SerieMensal = Schemas["SerieMensalDto"];
export type Sessao = Schemas["SessaoDto"];
export type VersaoPeiDetalhe = Schemas["VersaoPeiDetalheDto"];
export type UsuarioSessao = Schemas["UsuarioSessaoDto"];
export type UsuarioAdmin = Schemas["UsuarioAdminDto"];
export type AutorizacaoForaEscala = Schemas["AutorizacaoForaEscalaDto"];
export type SenhaTemporaria = Schemas["SenhaTemporariaDto"];
export type EventoAuditoria = Schemas["EventoAuditoriaDto"];

/** Corpo JSON de um requestBody do esquema OpenAPI. */
export type Json<T> = T extends { content: { "application/json": infer C } } ? C : never;

export type PagedResult<T> = {
  itens: T[];
  pagina: number;
  tamanho: number;
  total: number;
};
