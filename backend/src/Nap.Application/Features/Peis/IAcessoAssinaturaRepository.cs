using Nap.Domain.Peis;

namespace Nap.Application.Features.Peis;

public interface IAcessoAssinaturaRepository
{
    Task<AcessoAssinaturaPei?> ObterPorTokenHashAsync(string tokenHash, CancellationToken ct);
    /// <summary>Invalida os acessos não usados nem invalidados da versão; grava no próximo SalvarAsync.</summary>
    Task InvalidarAbertosDaVersaoAsync(Guid versaoId, DateTimeOffset agora, CancellationToken ct);
    void Adicionar(AcessoAssinaturaPei a);
    /// <summary>Relê o acesso do banco após conflito de concorrência; falso se ele não existe mais.</summary>
    Task<bool> RecarregarAsync(AcessoAssinaturaPei a, CancellationToken ct);
}
