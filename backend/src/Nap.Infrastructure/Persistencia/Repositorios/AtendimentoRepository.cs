using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Atendimentos;
using Nap.Domain.Atendimentos;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class AtendimentoRepository(NapDbContext db) : IAtendimentoRepository
{
    public Task<Atendimento?> ObterAsync(Guid id, CancellationToken ct) =>
        db.Atendimentos.Include(a => a.Correcoes).SingleOrDefaultAsync(a => a.Id == id, ct);

    public Task<bool> ExisteParaCompromissoAsync(Guid compromissoId, CancellationToken ct) =>
        db.Atendimentos.AnyAsync(a => a.CompromissoId == compromissoId, ct);

    public void Adicionar(Atendimento a) => db.Atendimentos.Add(a);
}
