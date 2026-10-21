using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nap.Application.Comum;
using StackExchange.Redis;

namespace Nap.Infrastructure.Cache;

/// <summary>
/// Cache de leituras do painel em Redis. Chave <c>nap:painel:{geracao}:{escopo}:{dia}:{hash16(filtros)}</c>, TTL 5 min.
/// Invalidar = <c>INCR nap:geracao:painel</c> (as chaves antigas ficam órfãs e expiram sozinhas).
/// O dia civil local entra em toda chave para que "hoje" (agenda do dia, semana parcial) não atravesse a meia-noite.
/// Redis é otimização: qualquer falha vira log de aviso e a consulta vai direto à fonte.
/// </summary>
public sealed class CacheConsultasRedis(IConnectionMultiplexer redis, IRelogio relogio, ILogger<CacheConsultasRedis> logger) : ICacheConsultas
{
    private const string Area = "painel";
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);

    public async Task<T> ObterOuCriarAsync<T>(string escopo, object filtros, Func<CancellationToken, Task<T>> fonte, CancellationToken ct)
    {
        IDatabase banco;
        string chave;
        try
        {
            banco = redis.GetDatabase();
            var geracao = (long?)await banco.StringGetAsync($"nap:geracao:{Area}") ?? 0;
            var hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(filtros, filtros.GetType())))[..16];
            chave = $"nap:{Area}:{geracao}:{escopo}:{relogio.Hoje:yyyy-MM-dd}:{hash}";
            var salvo = await banco.StringGetAsync(chave);
            if (salvo.HasValue)
                return JsonSerializer.Deserialize<T>((byte[])salvo!)!;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Cache de consultas indisponível na área {Area}; consultando a fonte", Area);
            return await fonte(ct);
        }

        var valor = await fonte(ct);
        try
        {
            await banco.StringSetAsync(chave, JsonSerializer.SerializeToUtf8Bytes(valor), Validade);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Falha ao gravar no cache de consultas da área {Area}", Area);
        }
        return valor;
    }

    public async Task InvalidarAreaAsync(CancellationToken ct)
    {
        try
        {
            await redis.GetDatabase().StringIncrementAsync($"nap:geracao:{Area}");
        }
        catch (Exception e)
        {
            // Nunca lança: a escrita já foi gravada; no pior caso a leitura fica até 5 min defasada.
            logger.LogWarning(e, "Falha ao invalidar o cache de consultas da área {Area}", Area);
        }
    }
}
