using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comunicacoes;

namespace Nap.Application.Features.Comunicacoes.Commands;

public record ResponderMensagemCommand(Guid MensagemId, string Corpo);

public class ResponderMensagemValidator : AbstractValidator<ResponderMensagemCommand>
{
    public ResponderMensagemValidator() => RuleFor(x => x.MensagemId).NotEmpty();
}

public sealed class ResponderMensagemHandler(IValidator<ResponderMensagemCommand> validador, IMensagemRepository mensagens,
    IAlunoRepository alunos, IEncaminhamentoRepository encaminhamentos, IUsuarioQueries usuarios, IUsuarioAtual usuario,
    IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task<Guid> Handle(ResponderMensagemCommand comando, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);

        var original = await mensagens.ObterAsync(comando.MensagemId, ct);
        if (original is null || !RegrasComunicacao.PodeVer(usuario, original)) throw new NotFoundException("Mensagem");

        // O outro participante precisa continuar podendo receber pelos vínculos da original (com o curso atual do aluno);
        // se não puder, a resposta é bloqueada, nunca redirecionada.
        var v = await RegrasComunicacao.VinculosDaOriginalAsync(original, alunos, encaminhamentos, ct);
        var resposta = Mensagem.CriarResposta(original, usuario.Id, comando.Corpo, relogio.Agora, v.CursoId);
        await RegrasComunicacao.ValidarDestinatarioAsync(usuario, resposta.DestinatarioId!.Value, v, usuarios, ct);

        mensagens.Adicionar(resposta);
        auditoria.Registrar("mensagem.responder", "Mensagem", resposta.Id);
        await uow.SalvarAsync(ct);
        return resposta.Id;
    }
}
