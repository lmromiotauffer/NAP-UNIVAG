using Nap.Application.Comum;
using Nap.Application.Features.Notificacoes;
using Nap.Domain.Comunicacoes;

namespace Nap.Application.Features.Comunicacoes.Commands;

public record MarcarMensagemLidaCommand(Guid Id);

public sealed class MarcarMensagemLidaHandler(IMensagemRepository mensagens, INotificacaoRepository notificacoes, IUsuarioAtual usuario,
    IRelogio relogio, IUnidadeDeTrabalho uow)
{
    public async Task Handle(MarcarMensagemLidaCommand comando, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);

        var m = await mensagens.ObterAsync(comando.Id, ct);
        if (m is null || !RegrasComunicacao.PodeVer(usuario, m)) throw new NotFoundException("Mensagem");
        if (m.DestinatarioId != usuario.Id) throw new ForbiddenException();

        m.MarcarLida(usuario.Id, relogio.Agora);
        // A notificação "Nova mensagem" também conta no badge: ler a mensagem a dá por lida.
        foreach (var n in await notificacoes.ListarNaoLidasAsync(usuario.Id, nameof(MensagemEnviada), $"/comunicacoes/{m.Id}", ct))
            n.MarcarLida(relogio.Agora);
        await uow.SalvarAsync(ct);
    }
}
