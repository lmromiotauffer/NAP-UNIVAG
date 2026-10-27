using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Notificacoes;
using Nap.Application.Features.Notificacoes.Commands;
using Nap.Domain.Comunicacoes;

namespace Nap.Application.Features.Comunicacoes.Commands;

public record MarcarMensagemLidaCommand(Guid Id);

public sealed class MarcarMensagemLidaHandler(IMensagemRepository mensagens, IAlunoRepository alunos,
    IEncaminhamentoRepository encaminhamentos, INotificacaoRepository notificacoes, IUsuarioAtual usuario, IRelogio relogio,
    IUnidadeDeTrabalho uow)
{
    public async Task Handle(MarcarMensagemLidaCommand comando, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);

        var m = await mensagens.ObterAsync(comando.Id, ct);
        if (m is null || !await RegrasComunicacao.PodeVerHojeAsync(usuario, m, alunos, encaminhamentos, ct))
            throw new NotFoundException("Mensagem");
        if (m.DestinatarioId != usuario.Id) throw new ForbiddenException();

        // Ler a mensagem dá por lida a notificação "Nova mensagem" dela.
        foreach (var n in await notificacoes.ListarNaoLidasAsync(usuario.Id, nameof(MensagemEnviada), GerarNotificacoesHandler.LinkMensagem(m.Id), ct))
            n.MarcarLida(relogio.Agora);
        await RegrasComunicacao.SalvarRelendoSeConflitarAsync(mensagens, uow, m, () => m.MarcarLida(usuario.Id, relogio.Agora), ct);
    }
}
