using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Peis;
using Nap.Domain.Peis;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class PeiRepository(NapDbContext db) : IPeiRepository
{
    public Task<Pei?> ObterAsync(Guid peiId, CancellationToken ct) => Completo().SingleOrDefaultAsync(p => p.Id == peiId, ct);

    public Task<Pei?> ObterPorVersaoAsync(Guid versaoId, CancellationToken ct) =>
        Completo().SingleOrDefaultAsync(p => p.Versoes.Any(v => v.Id == versaoId), ct);

    public void Adicionar(Pei p) => db.Peis.Add(p);

    public void RemoverVersao(VersaoPei v) => db.Remove(v);

    private IQueryable<Pei> Completo() => db.Peis.AsSplitQuery()
        .Include(p => p.Versoes).ThenInclude(v => v.Assinaturas)
        .Include(p => p.Versoes).ThenInclude(v => v.Ciencias);
}
