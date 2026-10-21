using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Peis;
using Nap.Domain.Peis;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class AcessoAssinaturaRepository(NapDbContext db) : IAcessoAssinaturaRepository
{
    public Task<AcessoAssinaturaPei?> ObterPorTokenHashAsync(string tokenHash, CancellationToken ct) =>
        db.AcessosAssinaturaPei.SingleOrDefaultAsync(a => a.TokenHash == tokenHash, ct);

    // Rastreados e alterados pelo domínio: a invalidação grava no mesmo SaveChanges da operação.
    public async Task InvalidarAbertosDaVersaoAsync(Guid versaoId, DateTimeOffset agora, CancellationToken ct)
    {
        var abertos = await db.AcessosAssinaturaPei
            .Where(a => a.VersaoId == versaoId && a.UsadoEm == null && a.InvalidadoEm == null).ToListAsync(ct);
        foreach (var a in abertos) a.Invalidar(agora);
    }

    public void Adicionar(AcessoAssinaturaPei a) => db.AcessosAssinaturaPei.Add(a);

    public async Task<bool> RecarregarAsync(AcessoAssinaturaPei a, CancellationToken ct)
    {
        var entrada = db.Entry(a);
        await entrada.ReloadAsync(ct);
        return entrada.State != EntityState.Detached;
    }
}
