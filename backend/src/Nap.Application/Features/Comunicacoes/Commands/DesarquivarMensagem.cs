using Nap.Application.Comum;

namespace Nap.Application.Features.Comunicacoes.Commands;

public record DesarquivarMensagemCommand(Guid Id);

public sealed class DesarquivarMensagemHandler(IMensagemRepository mensagens, IUsuarioAtual usuario, IUnidadeDeTrabalho uow)
{
    public async Task Handle(DesarquivarMensagemCommand comando, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);

        var m = await mensagens.ObterAsync(comando.Id, ct);
        if (m is null || !RegrasComunicacao.PodeVer(usuario, m)) throw new NotFoundException("Mensagem");

        m.Desarquivar(usuario.Id);
        await uow.SalvarAsync(ct);
    }
}
