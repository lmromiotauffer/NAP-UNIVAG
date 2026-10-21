using Nap.Application.Features.Cursos;

namespace Nap.Application.Tests.Fakes;

public sealed class CursoQueriesFalso(params Guid[] cursosAtivos) : ICursoQueries
{
    public List<CursoDto> Cursos { get; } = [];

    public Task<IReadOnlyList<CursoDto>> ListarAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CursoDto>>(Cursos);

    public Task<bool> CursoAtivoExisteAsync(Guid id, CancellationToken ct) => Task.FromResult(cursosAtivos.Contains(id));
}
