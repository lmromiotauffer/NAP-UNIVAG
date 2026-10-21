using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Encaminhamentos;
using Nap.Domain.Encaminhamentos;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class EncaminhamentoRepository(NapDbContext db) : IEncaminhamentoRepository
{
    public Task<Encaminhamento?> ObterAsync(Guid id, CancellationToken ct) =>
        db.Encaminhamentos.Include(e => e.Evolucoes).SingleOrDefaultAsync(e => e.Id == id, ct);

    public void Adicionar(Encaminhamento e) => db.Encaminhamentos.Add(e);
}
