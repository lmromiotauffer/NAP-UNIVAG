using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Cursos;
using Nap.Domain.Cursos;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class CursoRepository(NapDbContext db) : ICursoRepository
{
    public Task<Curso?> ObterAsync(Guid id, CancellationToken ct) => db.Cursos.SingleOrDefaultAsync(c => c.Id == id, ct);

    // A collation padrão do SQL Server já ignora caixa (o índice único também).
    public Task<bool> NomeExisteAsync(string nome, Guid? ignorarId, CancellationToken ct) =>
        db.Cursos.AnyAsync(c => c.Nome == nome && (ignorarId == null || c.Id != ignorarId), ct);

    public void Adicionar(Curso c) => db.Cursos.Add(c);
}
