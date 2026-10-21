namespace Nap.Application.Comum;

public interface ICacheConsultas
{
    Task<T> ObterOuCriarAsync<T>(string escopo, object filtros, Func<CancellationToken, Task<T>> fonte, CancellationToken ct);

    Task InvalidarAreaAsync(CancellationToken ct);
}
