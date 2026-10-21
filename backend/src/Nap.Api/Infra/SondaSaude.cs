using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Nap.Api.Infra;

/// <summary>Health check a partir de uma sonda. Falso ou exceção = <c>FailureStatus</c> do registro.
/// Dependências são resolvidas dentro da sonda: falha de configuração vira status, não erro 500.</summary>
public sealed class SondaSaude(Func<CancellationToken, Task<bool>> sonda) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext contexto, CancellationToken ct = default) =>
        await sonda(ct) ? HealthCheckResult.Healthy() : new HealthCheckResult(contexto.Registration.FailureStatus);
}
