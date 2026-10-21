using Nap.Application.Comum;

namespace Nap.Application.Features.Notificacoes.Commands;

public record MarcarNotificacaoLidaCommand(Guid Id);

public sealed class MarcarNotificacaoLidaHandler(INotificacaoRepository notificacoes, IUsuarioAtual usuario, IRelogio relogio,
    IUnidadeDeTrabalho uow)
{
    public async Task Handle(MarcarNotificacaoLidaCommand comando, CancellationToken ct = default)
    {
        var n = await notificacoes.ObterAsync(comando.Id, ct);
        // A de outro usuário é tratada como inexistente.
        if (n is null || n.DestinatarioId != usuario.Id) throw new NotFoundException("Notificação");
        n.MarcarLida(relogio.Agora);
        await uow.SalvarAsync(ct);
    }
}
