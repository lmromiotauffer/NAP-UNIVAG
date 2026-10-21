using Nap.Application.Comum;

namespace Nap.Application.Features.Comunicacoes.Commands;

public record DescartarRascunhoCommand(Guid Id);

/// <summary>Exclusão física: rascunho não tem histórico a preservar.</summary>
public sealed class DescartarRascunhoHandler(IMensagemRepository mensagens, IUsuarioAtual usuario, IUnidadeDeTrabalho uow)
{
    public async Task Handle(DescartarRascunhoCommand comando, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);

        var m = await mensagens.ObterAsync(comando.Id, ct);
        if (m is null || m.AutorId != usuario.Id) throw new NotFoundException("Mensagem");
        m.ExigirRascunho();

        mensagens.Remover(m);
        await uow.SalvarAsync(ct);
    }
}
