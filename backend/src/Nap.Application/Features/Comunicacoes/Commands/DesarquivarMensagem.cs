using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Encaminhamentos;

namespace Nap.Application.Features.Comunicacoes.Commands;

public record DesarquivarMensagemCommand(Guid Id);

public sealed class DesarquivarMensagemHandler(IMensagemRepository mensagens, IAlunoRepository alunos,
    IEncaminhamentoRepository encaminhamentos, IUsuarioAtual usuario, IUnidadeDeTrabalho uow)
{
    public async Task Handle(DesarquivarMensagemCommand comando, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);

        var m = await mensagens.ObterAsync(comando.Id, ct);
        if (m is null || !await RegrasComunicacao.PodeVerHojeAsync(usuario, m, alunos, encaminhamentos, ct))
            throw new NotFoundException("Mensagem");

        await RegrasComunicacao.SalvarRelendoSeConflitarAsync(mensagens, uow, m, () => m.Desarquivar(usuario.Id), ct);
    }
}
