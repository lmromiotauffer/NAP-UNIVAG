namespace Nap.Application.Features.Painel;

public record PeriodoDto(DateOnly Inicio, DateOnly Fim, bool Parcial);

public record ProducaoSemanaDto(int AtendimentosRealizados, int MinutosRealizados, int Individuais, int Faltas,
    decimal? TaxaAusencia, string? MotivoNaoCalculado, int CompromissosSemDesfecho);

public record SerieMensalDto(int Ano, int Mes, int? Realizados, int? Minutos, bool Parcial);

public record ContagemDto(string Chave, string Rotulo, int Total);

public record AlertaDto(string Tipo, string Titulo, string Detalhe, Guid? AlunoId, int? DiasOuOcorrencias);

public record ProducaoProfissionalDto(Guid Id, string Nome, int Realizados, int Faltas, int Minutos);

public record PainelDto(PeriodoDto Periodo, ProducaoSemanaDto? Semana, IReadOnlyList<Agenda.CompromissoDto>? AgendaHoje,
    IReadOnlyList<SerieMensalDto>? Ultimos6Meses, IReadOnlyList<ContagemDto>? AlunosPorCurso,
    IReadOnlyList<ContagemDto>? AlunosPorSituacao, IReadOnlyList<AlertaDto>? Alertas,
    IReadOnlyList<ProducaoProfissionalDto>? PorProfissional, EncaminhamentosPainelDto? Encaminhamentos,
    Peis.PeisPainelDto? Peis = null, AtividadesPainelDto? Atividades = null);

/// <summary>Atividades nos cursos do mês civil corrente (fuso institucional); <c>Recentes</c> = até 5, mais recentes primeiro.</summary>
public record AtividadesPainelDto(int NoMes, int MinutosNoMes, IReadOnlyList<AtividadeResumoDto> Recentes);

public record AtividadeResumoDto(Guid Id, DateTimeOffset Inicio, Domain.Atividades.Turno Turno, int DuracaoMinutos, string? Curso, string Profissional,
    Domain.Atividades.TipoAtividade Tipo, string Descricao);

/// <summary>Contagens sobre encaminhamentos não encerrados; os limiares vêm de <c>ConfiguracaoAlertas</c>.</summary>
public record EncaminhamentosPainelDto(int Abertos, int AguardandoAcolhimento, int EmAtendimento, int SemAtualizacao, int SemAcolhimentoNoPrazo,
    int DiasSemAtualizacao = 0);

/// <summary>Números brutos da semana; a taxa de ausência é calculada no handler.</summary>
public record SemanaBrutaDto(int Realizados, int Minutos, int Individuais, int Faltas, int CompromissosSemDesfecho);
