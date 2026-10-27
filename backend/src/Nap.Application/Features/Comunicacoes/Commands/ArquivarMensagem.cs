using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Encaminhamentos;

namespace Nap.Application.Features.Comunicacoes.Commands;

/// <summary>Arquiva só para quem pediu; o outro participante continua com a mensagem na caixa dele.</summary>
public record ArquivarMensagemCommand(Guid Id);

public sealed class ArquivarMensagemHandler(IMensagemRepository mensagens, IAlunoRepository alunos,
    IEncaminhamentoRepository encaminhamentos, IUsuarioAtual usuario, IRelogio relogio, IUnidadeDeTrabalho uow)
{
    public async Task Handle(ArquivarMensagemCommand comando, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);

        var m = await mensagens.ObterAsync(comando.Id, ct);
        if (m is null || !await RegrasComunicacao.PodeVerHojeAsync(usuario, m, alunos, encaminhamentos, ct))
            throw new NotFoundException("Mensagem");

        await RegrasComunicacao.SalvarRelendoSeConflitarAsync(mensagens, uow, m, () => m.Arquivar(usuario.Id, relogio.Agora), ct);
    }
}
