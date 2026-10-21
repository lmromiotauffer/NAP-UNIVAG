using Nap.Application.Comum;

namespace Nap.Application.Tests.Fakes;

public sealed class CacheConsultasFalso(List<string>? ordem = null) : ICacheConsultas
{
    public List<(string Escopo, object Filtros)> Consultas { get; } = [];
    public int Invalidacoes { get; private set; }

    public Task<T> ObterOuCriarAsync<T>(string escopo, object filtros, Func<CancellationToken, Task<T>> fonte, CancellationToken ct)
    {
        Consultas.Add((escopo, filtros));
        return fonte(ct);
    }

    public Task InvalidarAreaAsync(CancellationToken ct)
    {
        Invalidacoes++;
        ordem?.Add("invalidar");
        return Task.CompletedTask;
    }
}
