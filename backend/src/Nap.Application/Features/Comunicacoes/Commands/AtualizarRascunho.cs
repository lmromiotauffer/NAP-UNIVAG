using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Cursos;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comunicacoes;

namespace Nap.Application.Features.Comunicacoes.Commands;

/// <summary>Mesmas validações de <see cref="CriarRascunhoCommand"/>.</summary>
public record AtualizarRascunhoCommand(Guid Id, TipoMensagem Tipo, string Assunto, string Corpo, Guid? DestinatarioId,
    Guid? CursoId, Guid? AlunoId, Guid? EncaminhamentoId, string Versao);

public class AtualizarRascunhoValidator : AbstractValidator<AtualizarRascunhoCommand>
{
    public AtualizarRascunhoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.Assunto).NotEmpty().WithMessage("Informe o assunto.").Length(3, 200);
        RuleFor(x => x.Corpo).NotEmpty().WithMessage("Informe o texto da mensagem.").MaximumLength(10000);
    }
}

public sealed class AtualizarRascunhoHandler(IValidator<AtualizarRascunhoCommand> validador, IMensagemRepository mensagens,
    IAlunoRepository alunos, IEncaminhamentoRepository encaminhamentos, ICursoQueries cursos, IUsuarioQueries usuarios,
    IUsuarioAtual usuario, IRelogio relogio, IUnidadeDeTrabalho uow)
{
    public async Task Handle(AtualizarRascunhoCommand comando, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);

        var m = await mensagens.ObterAsync(comando.Id, ct);
        // Só o autor edita: para qualquer outro (inclusive o destinatário) a mensagem não existe.
        if (m is null || m.AutorId != usuario.Id) throw new NotFoundException("Mensagem");
        uow.ExigirVersao(m, comando.Versao);
        m.ExigirRascunho();
        var v = await RegrasComunicacao.ValidarVinculosAsync(usuario, comando.CursoId, comando.AlunoId, comando.EncaminhamentoId,
            alunos, encaminhamentos, cursos, ct);
        await RegrasComunicacao.ValidarDestinatarioDoRascunhoAsync(usuario, comando.DestinatarioId, usuarios, ct);

        m.AtualizarRascunho(comando.Tipo, comando.Assunto, comando.Corpo, comando.DestinatarioId, v.CursoId, v.AlunoId,
            v.EncaminhamentoId, relogio.Agora);
        await uow.SalvarAsync(ct);
    }
}
