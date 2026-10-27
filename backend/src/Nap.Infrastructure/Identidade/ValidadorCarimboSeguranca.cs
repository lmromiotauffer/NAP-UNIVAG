using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Nap.Application.Comum;
using Nap.Infrastructure.Persistencia;

namespace Nap.Infrastructure.Identidade;

/// <summary>
/// Confere, a cada requisição autenticada: o carimbo (<c>stamp</c>) do token ainda é o atual, o usuário está ativo e a
/// sessão (<c>sid</c> = família de refresh) não foi encerrada. Cache de 30 s no Redis; Redis indisponível → consulta ao
/// banco. Nunca autoriza por padrão.
/// </summary>
public sealed class ValidadorCarimboSeguranca(NapDbContext db, IDistributedCache cache, IRelogio relogio,
    ILogger<ValidadorCarimboSeguranca> logger)
{
    private static readonly DistributedCacheEntryOptions Validade = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) };

    public static string Chave(Guid usuarioId) => $"nap:stamp:{usuarioId:N}";

    public static string ChaveSessao(Guid familiaId) => $"nap:sid:{familiaId:N}";

    public async Task<bool> ValidarAsync(Guid usuarioId, string stamp, Guid familiaId, CancellationToken ct)
    {
        var carimbo = await ObterAsync(Chave(usuarioId), async () =>
        {
            var usuario = await db.Users.AsNoTracking().Where(u => u.Id == usuarioId)
                .Select(u => new { u.SecurityStamp, u.Ativo }).SingleOrDefaultAsync(ct);
            return usuario?.SecurityStamp is null ? null : $"{usuario.SecurityStamp}|{usuario.Ativo}";
        }, ct);
        if (carimbo != $"{stamp}|{bool.TrueString}")
            return false;

        var agora = relogio.Agora;
        var sessao = await ObterAsync(ChaveSessao(familiaId), async () =>
            await db.TokensRefresh.AnyAsync(t => t.FamiliaId == familiaId && t.UsuarioId == usuarioId
                && t.RevogadoEm == null && t.ExpiraEm > agora, ct) ? $"{usuarioId:N}|ativa" : "encerrada", ct);
        return sessao == $"{usuarioId:N}|ativa";
    }

    /// <summary>Valor em cache; ausente ou Redis com erro → banco (e regrava no cache se o Redis respondeu).</summary>
    private async Task<string?> ObterAsync(string chave, Func<Task<string?>> doBanco, CancellationToken ct)
    {
        try
        {
            if (await cache.GetStringAsync(chave, ct) is { } valor)
                return valor;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Redis indisponível ao validar a sessão; consultando o banco");
            return await doBanco();
        }

        var atual = await doBanco();
        if (atual is null) return null;
        // ao cache e vale até o TTL (30 s). Mesmo limite da remoção que falha; reduzir o TTL se não for aceitável.
        try { await cache.SetStringAsync(chave, atual, Validade, ct); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Falha ao gravar a validação de sessão no Redis");
        }
        return atual;
    }

    /// <summary>Descarta a validação em cache da sessão encerrada (logout ou família revogada).</summary>
    public Task EsquecerSessaoAsync(Guid familiaId, CancellationToken ct) => RemoverAsync(ChaveSessao(familiaId), ct);

    /// <summary>Descarta o carimbo em cache do usuário (carimbo rotacionado ou situação alterada).</summary>
    public Task EsquecerUsuarioAsync(Guid usuarioId, CancellationToken ct) => RemoverAsync(Chave(usuarioId), ct);

    private async Task RemoverAsync(string chave, CancellationToken ct)
    {
        try { await cache.RemoveAsync(chave, ct); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Falha ao remover a validação de sessão do Redis; a entrada expira em até 30 s");
        }
    }
}
