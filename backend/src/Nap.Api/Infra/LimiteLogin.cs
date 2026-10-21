using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Nap.Api.Infra;

/// <summary>Limite de tentativas de login por IP (seção <c>Nap:LimiteLogin</c>): janela fixa, sem fila.</summary>
public sealed class OpcoesLimiteLogin
{
    public int Tentativas { get; set; } = 10;
    public int JanelaSegundos { get; set; } = 60;
}

/// <summary>
/// Cada login recusado grava um evento de auditoria; o limite impede que o endpoint anônimo vire inserção ilimitada.
/// Partição pelo IP da conexão: sem forwarded headers, atrás de proxy todos dividem o IP do proxy.
/// </summary>
public static class LimiteLogin
{
    public const string Politica = "login";

    public static void Configurar(RateLimiterOptions o)
    {
        // Opções lidas por requisição: cada host (inclusive os de teste) usa a própria configuração.
        o.AddPolicy(Politica, http =>
        {
            var opcoes = http.RequestServices.GetRequiredService<IOptions<OpcoesLimiteLogin>>().Value;
            // Sem IP (TestServer), uma partição única: a chave não pode ser nula.
            return RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "sem-ip",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = opcoes.Tentativas,
                    Window = TimeSpan.FromSeconds(opcoes.JanelaSegundos),
                    QueueLimit = 0,
                });
        });
        o.OnRejected = (contexto, _) =>
        {
            var http = contexto.HttpContext;
            if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                http.Response.Headers.RetryAfter = Math.Ceiling(espera.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            return new ValueTask(ManipuladorExcecoes.EscreverAsync(http, StatusCodes.Status429TooManyRequests,
                "Muitas tentativas de login. Aguarde um instante e tente novamente."));
        };
    }
}
