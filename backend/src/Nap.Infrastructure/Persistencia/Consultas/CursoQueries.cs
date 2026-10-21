using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Cursos;

namespace Nap.Infrastructure.Persistencia.Consultas;

public sealed class CursoQueries(NapDbContext db) : ICursoQueries
{
    public async Task<IReadOnlyList<CursoDto>> ListarAsync(CancellationToken ct) =>
        await db.Cursos.AsNoTracking()
            .OrderBy(c => c.Nome).ThenBy(c => c.Id)
            .Select(c => new CursoDto(c.Id, c.Nome, c.Ativo, Convert.ToBase64String(EF.Property<byte[]>(c, "Versao"))))
            .ToListAsync(ct);

    public Task<bool> CursoAtivoExisteAsync(Guid id, CancellationToken ct) =>
        db.Cursos.AnyAsync(c => c.Id == id && c.Ativo, ct);
}
