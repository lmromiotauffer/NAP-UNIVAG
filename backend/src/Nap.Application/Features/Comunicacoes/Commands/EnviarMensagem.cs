using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Cursos;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comunicacoes;

namespace Nap.Application.Features.Comunicacoes.Commands;

/// <summary>Com <c>RascunhoId</c>, atualiza e envia o rascunho do próprio autor; sem, cria e envia.
/// <c>Versao</c> (If-Match, opcional): com ela, o envio recusa um rascunho alterado em outra aba.</summary>
public record EnviarMensagemCommand(Guid? RascunhoId, Guid DestinatarioId, TipoMensagem Tipo, string Assunto, string Corpo,
    Guid? CursoId, Guid? AlunoId, Guid? EncaminhamentoId, string? Versao = null);

public class EnviarMensagemValidator : AbstractValidator<EnviarMensagemCommand>
{
    public EnviarMensagemValidator()
    {
        RuleFor(x => x.DestinatarioId).NotEmpty().WithMessage("Informe o destinatário.");
        RuleFor(x => x.Assunto).NotEmpty().WithMessage("Informe o assunto.").Length(3, 200);
        RuleFor(x => x.Corpo).NotEmpty().WithMessage("Informe o texto da mensagem.").MaximumLength(10000);
        RuleFor(x => x.Tipo).IsInEnum()
            // EnvioPei nasce só da publicação do PEI (PublicarPeiHandler), com o vínculo à versão publicada.
            .NotEqual(TipoMensagem.EnvioPei).WithMessage("O envio de PEI é gerado só pela publicação do PEI.");
        RuleFor(x => x.EncaminhamentoId).NotEmpty().When(x => x.Tipo == TipoMensagem.RetornoEncaminhamento)
            .WithMessage("Informe o encaminhamento.");
    }
}

public sealed class EnviarMensagemHandler(IValidator<EnviarMensagemCommand> validador, IMensagemRepository mensagens,
    IAlunoRepository alunos, IEncaminhamentoRepository encaminhamentos, ICursoQueries cursos, IUsuarioQueries usuarios,
    IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task<Guid> Handle(EnviarMensagemCommand comando, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);

        Mensagem? rascunho = null;
        if (comando.RascunhoId is { } rascunhoId)
        {
            rascunho = await mensagens.ObterAsync(rascunhoId, ct);
            if (rascunho is null || rascunho.AutorId != usuario.Id) throw new NotFoundException("Mensagem");
            rascunho.ExigirRascunho();
            if (comando.Versao is { } versao) uow.ExigirVersao(rascunho, versao);
        }

        var v = await RegrasComunicacao.ValidarVinculosAsync(usuario, comando.CursoId, comando.AlunoId, comando.EncaminhamentoId,
            alunos, encaminhamentos, cursos, ct);
        await RegrasComunicacao.ValidarDestinatarioAsync(usuario, comando.DestinatarioId, v, usuarios, ct);

        var agora = relogio.Agora;
        var m = rascunho;
        if (m is null)
        {
            m = Mensagem.CriarRascunho(usuario.Id, comando.Tipo, comando.Assunto, comando.Corpo, comando.DestinatarioId,
                v.CursoId, v.AlunoId, v.EncaminhamentoId, agora);
            mensagens.Adicionar(m);
        }
        else
            m.AtualizarRascunho(comando.Tipo, comando.Assunto, comando.Corpo, comando.DestinatarioId, v.CursoId, v.AlunoId,
                v.EncaminhamentoId, agora);
        m.Enviar(agora);
        auditoria.Registrar("mensagem.enviar", "Mensagem", m.Id);
        await uow.SalvarAsync(ct);
        return m.Id;
    }
}
