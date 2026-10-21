using Nap.Application.Comum;
using Nap.Domain.Atividades;

namespace Nap.Application.Features.Atividades;

/// <param name="CursoRecorte">Quando preenchido, só atividades desse curso (coordenador de curso); aplicado antes de paginar.</param>
public record FiltroAtividades(DateOnly? Inicio, DateOnly? Fim, Guid? CursoId, Guid? ProfissionalId, TipoAtividade? Tipo,
    bool? Institucionais, Guid? CursoRecorte, int Pagina, int Tamanho);

public interface IAtividadeQueries
{
    Task<PagedResult<AtividadeDto>> ListarAsync(FiltroAtividades f, CancellationToken ct);

    /// <summary>Nulo quando não existe ou está fora de <paramref name="cursoRecorte"/>; <c>PodeCorrigir</c> vem falso (decidido no handler).</summary>
    Task<AtividadeDetalheDto?> ObterAsync(Guid id, Guid? cursoRecorte, CancellationToken ct);
}
