using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Nap.Api.Infra;

/// <summary>Limites por IP das rotas anônimas (seção <c>Nap:LimiteLogin</c>): janela fixa, sem fila.</summary>
public sealed class OpcoesLimiteLogin
{
    public int Tentativas { get; set; } = 10;
    public int JanelaSegundos { get; set; } = 60;

    /// <summary>Assinatura do aluno (<c>/api/assinar</c>): abrir, assinar e alguns erros de código cabem com folga.</summary>
    public int TentativasAssinatura { get; set; } = 30;
}

/// <summary>
/// Cada login recusado grava um evento de auditoria; o limite impede que o endpoint anônimo vire inserção ilimitada.
/// Partição pelo IP da conexão: sem forwarded headers, atrás de proxy todos dividem o IP do proxy.
/// </summary>
public static class LimiteLogin
{
    public const string Politica = "login";

    /// <summary>Rotas anônimas da assinatura do aluno: cada chamada custa uma consulta ao banco.</summary>
    public const string PoliticaAssinatura = "assinatura";

    public static void Configurar(RateLimiterOptions o)
    {
        AdicionarPorIp(o, Politica, opcoes => opcoes.Tentativas);
        AdicionarPorIp(o, PoliticaAssinatura, opcoes => opcoes.TentativasAssinatura);
        o.OnRejected = (contexto, _) =>
        {
            var http = contexto.HttpContext;
            if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                http.Response.Headers.RetryAfter = Math.Ceiling(espera.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            return new ValueTask(ManipuladorExcecoes.EscreverAsync(http, StatusCodes.Status429TooManyRequests,
                "Muitas tentativas. Aguarde um instante e tente novamente."));
        };
    }

    private static void AdicionarPorIp(RateLimiterOptions o, string politica, Func<OpcoesLimiteLogin, int> limite) =>
        // Opções lidas por requisição: cada host (inclusive os de teste) usa a própria configuração.
        o.AddPolicy(politica, http =>
        {
            var opcoes = http.RequestServices.GetRequiredService<IOptions<OpcoesLimiteLogin>>().Value;
            // Sem IP (TestServer), uma partição única: a chave não pode ser nula.
            return RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "sem-ip",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limite(opcoes),
                    Window = TimeSpan.FromSeconds(opcoes.JanelaSegundos),
                    QueueLimit = 0,
                });
        });
}
