namespace Nap.Application.Comum;

public interface IUnidadeDeTrabalho
{
    Task SalvarAsync(CancellationToken ct);
    /// <summary>Lança conflito se a versão recebida (If-Match) não for a que a entidade tinha ao ser carregada.</summary>
    void ExigirVersao(object entidade, string versaoEsperada);
    Task<T> EmTransacaoAsync<T>(Func<CancellationToken, Task<T>> operacao, CancellationToken ct);
}
