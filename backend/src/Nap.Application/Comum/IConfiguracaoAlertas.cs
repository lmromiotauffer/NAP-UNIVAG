using Nap.Application.Features.Configuracoes;
using Nap.Domain.Configuracoes;

namespace Nap.Application.Comum;

/// <summary>Limiares dos alertas, lidos do banco (linha única de configuração).</summary>
public interface IConfiguracaoAlertas
{
    Task<ConfiguracaoAlertasDto> ObterDtoAsync(CancellationToken ct);
    /// <summary>A linha rastreada, para edição.</summary>
    Task<ConfiguracaoAlertas> ObterParaEdicaoAsync(CancellationToken ct);
}
