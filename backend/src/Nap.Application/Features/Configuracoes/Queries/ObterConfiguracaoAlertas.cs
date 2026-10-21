using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Configuracoes.Queries;

public sealed class ObterConfiguracaoAlertasHandler(IConfiguracaoAlertas configuracao, IUsuarioAtual usuario)
{
    public Task<ConfiguracaoAlertasDto> Handle(CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        return configuracao.ObterDtoAsync(ct);
    }
}
