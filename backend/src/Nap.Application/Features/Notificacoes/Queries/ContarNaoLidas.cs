using Nap.Application.Comum;

namespace Nap.Application.Features.Notificacoes.Queries;

public sealed class ContarNaoLidasHandler(INotificacaoQueries consultas, IUsuarioAtual usuario)
{
    public async Task<ContagemNaoLidasDto> Handle(CancellationToken ct = default) =>
        new(await consultas.ContarNaoLidasAsync(usuario.Id, ct));
}
