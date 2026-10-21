namespace Nap.Application.Features.Cursos;

/// <summary><c>Versao</c> vai no ETag/If-Match de renomear e alterar situação.</summary>
public record CursoDto(Guid Id, string Nome, bool Ativo, string Versao);

public interface ICursoQueries
{
    Task<IReadOnlyList<CursoDto>> ListarAsync(CancellationToken ct);
    Task<bool> CursoAtivoExisteAsync(Guid id, CancellationToken ct);
}
