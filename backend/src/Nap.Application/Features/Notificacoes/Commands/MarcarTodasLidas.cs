using Nap.Application.Comum;

namespace Nap.Application.Features.Notificacoes.Commands;

public sealed class MarcarTodasLidasHandler(INotificacaoRepository notificacoes, IUsuarioAtual usuario, IRelogio relogio)
{
    public Task Handle(CancellationToken ct = default) => notificacoes.MarcarTodasLidasAsync(usuario.Id, relogio.Agora, ct);
}
