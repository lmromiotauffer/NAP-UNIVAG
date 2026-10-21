using Nap.Domain.Producao;

namespace Nap.Application.Features.Producao;

public record EventoFechamentoDto(TipoEventoFechamento Tipo, string Autor, DateTimeOffset Em, string? Justificativa);

public record SemanaProducaoDto(Guid ProfissionalId, string Profissional, DateOnly Semana, bool Fechada, int Pendentes, string? Versao,
    IReadOnlyList<EventoFechamentoDto> Eventos);

public record AjusteProducaoDto(Guid Id, Guid ProfissionalId, string Profissional, DateOnly Data, CategoriaProducao Categoria, string Rotulo,
    int? Ocorrencias, int? Minutos, string Justificativa, string Autor, DateTimeOffset Em);
