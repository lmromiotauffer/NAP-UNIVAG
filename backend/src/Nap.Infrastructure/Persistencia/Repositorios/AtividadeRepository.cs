using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Atividades;
using Nap.Domain.Atividades;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class AtividadeRepository(NapDbContext db) : IAtividadeRepository
{
    public Task<AtividadeCurso?> ObterAsync(Guid id, CancellationToken ct) =>
        db.AtividadesCurso.Include(a => a.Correcoes).SingleOrDefaultAsync(a => a.Id == id, ct);

    public Task<bool> ExisteParaCompromissoAsync(Guid compromissoId, CancellationToken ct) =>
        db.AtividadesCurso.AnyAsync(a => a.CompromissoId == compromissoId, ct);

    public void Adicionar(AtividadeCurso a) => db.AtividadesCurso.Add(a);
}
