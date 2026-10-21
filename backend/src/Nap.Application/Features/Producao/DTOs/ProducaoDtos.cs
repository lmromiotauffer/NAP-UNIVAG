using Nap.Domain.Agenda;
using Nap.Domain.Producao;

namespace Nap.Application.Features.Producao;

public enum TipoPeriodo { Semana, Mes, Semestre, Ano }

public record PeriodoProducaoDto(TipoPeriodo Tipo, DateOnly Inicio, DateOnly Fim, bool Parcial);

/// <summary>Ocorrências e minutos nunca se somam entre si; ocorrências nulas em "Horários disponíveis sem utilização".</summary>
public record ValorProducaoDto(int? Ocorrencias, int Minutos);

public record CategoriaProducaoDto(CategoriaProducao Chave, string Rotulo, ValorProducaoDto Registrado, ValorProducaoDto Ajustes,
    ValorProducaoDto Total);

/// <summary>Sem ajustes: Ociosidade = Capacidade − ProducaoNaEscala − AusenciasNaEscala − PendentesNaEscala, negativa quando inconsistente.</summary>
public record TempoDto(int Capacidade, int ProducaoNaEscala, int AusenciasNaEscala, int PendentesNaEscala, int Ociosidade, int TempoExtra,
    bool Inconsistente, IReadOnlyList<DateOnly> DiasInconsistentes);

public record TaxaDto(decimal? Valor, int Numerador, int Denominador, string? Motivo);

public record SerieProducaoDto(DateOnly Inicio, DateOnly Fim, bool Parcial, IReadOnlyList<CategoriaProducaoDto> Categorias);

/// <summary>Variação do Total por categoria; percentual (fração) nulo quando o anterior é zero.</summary>
public record VariacaoProducaoDto(CategoriaProducao Chave, ValorProducaoDto Anterior, ValorProducaoDto Diferenca, decimal? PercentualOcorrencias,
    decimal? PercentualMinutos);

public record ComparacaoDto(PeriodoProducaoDto Periodo, IReadOnlyList<VariacaoProducaoDto> Categorias);

public record ProducaoProfissionalLinhaDto(Guid ProfissionalId, string Profissional, IReadOnlyList<CategoriaProducaoDto> Categorias,
    TempoDto? Tempo, TaxaDto TaxaAusencia, int? Pendentes);

public record ProducaoDto(PeriodoProducaoDto Periodo, IReadOnlyList<CategoriaProducaoDto> Categorias, TempoDto? Tempo, TaxaDto TaxaAusencia,
    int? Pendentes, IReadOnlyList<SerieProducaoDto> Serie, ComparacaoDto Anterior, IReadOnlyList<ProducaoProfissionalLinhaDto>? PorProfissional);

// Fatos brutos do período, carregados em lote; o cálculo por dia é feito em memória pelo handler.
public record IntervaloProfissional(Guid ProfissionalId, DateTimeOffset Inicio, DateTimeOffset Fim);

public record OcorrenciaProfissional(Guid ProfissionalId, CategoriaProducao Categoria, DateTimeOffset Inicio, DateTimeOffset Fim, bool Individual);

public record FatosProducao(IReadOnlyList<EscalaProfissional> Escalas, IReadOnlyCollection<DateOnly> Feriados,
    IReadOnlyList<IntervaloProfissional> Bloqueios, IReadOnlyList<OcorrenciaProfissional> Ocorrencias,
    IReadOnlyList<IntervaloProfissional> Pendentes, IReadOnlyList<AjusteProducao> Ajustes);
