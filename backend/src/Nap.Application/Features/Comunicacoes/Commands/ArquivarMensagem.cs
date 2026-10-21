using Nap.Application.Comum;

namespace Nap.Application.Features.Comunicacoes.Commands;

/// <summary>Arquiva só para quem pediu; o outro participante continua com a mensagem na caixa dele.</summary>
public record ArquivarMensagemCommand(Guid Id);

public sealed class ArquivarMensagemHandler(IMensagemRepository mensagens, IUsuarioAtual usuario, IRelogio relogio,
    IUnidadeDeTrabalho uow)
{
    public async Task Handle(ArquivarMensagemCommand comando, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);

        var m = await mensagens.ObterAsync(comando.Id, ct);
        if (m is null || !RegrasComunicacao.PodeVer(usuario, m)) throw new NotFoundException("Mensagem");

        m.Arquivar(usuario.Id, relogio.Agora);
        await uow.SalvarAsync(ct);
    }
}
