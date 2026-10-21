using Nap.Domain.Atividades;

namespace Nap.Application.Features.Atividades;

public record AtividadeDto(Guid Id, DateOnly Data, Turno Turno, DateTimeOffset Inicio, DateTimeOffset Fim, int DuracaoMinutos,
    Guid? CursoId, string? Curso, Guid ProfissionalId, string Profissional, TipoAtividade Tipo, string Descricao, bool Avulsa,
    string Versao);

public record CorrecaoAtividadeDto(string Autor, DateTimeOffset Em, string Justificativa, DateTimeOffset InicioAnterior,
    DateTimeOffset FimAnterior, Guid? CursoIdAnterior, string? CursoAnterior, TipoAtividade TipoAnterior, string DescricaoAnterior);

public record AtividadeDetalheDto(Guid Id, DateOnly Data, Turno Turno, DateTimeOffset Inicio, DateTimeOffset Fim, int DuracaoMinutos,
    Guid? CursoId, string? Curso, Guid ProfissionalId, string Profissional, TipoAtividade Tipo, string Descricao, bool Avulsa,
    string Versao, string? JustificativaAvulso, Guid? CompromissoId, IReadOnlyList<CorrecaoAtividadeDto> Correcoes, bool PodeCorrigir);
