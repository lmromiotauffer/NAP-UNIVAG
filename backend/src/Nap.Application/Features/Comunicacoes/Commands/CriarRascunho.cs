using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Cursos;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comunicacoes;

namespace Nap.Application.Features.Comunicacoes.Commands;

/// <summary>
/// Rascunho já valida os vínculos (existem e o autor os vê) e que o destinatário existe e está ativo; se o destinatário pode
/// receber esta comunicação só é conferido no envio.
/// </summary>
public record CriarRascunhoCommand(TipoMensagem Tipo, string Assunto, string Corpo, Guid? DestinatarioId, Guid? CursoId,
    Guid? AlunoId, Guid? EncaminhamentoId);

public class CriarRascunhoValidator : AbstractValidator<CriarRascunhoCommand>
{
    public CriarRascunhoValidator()
    {
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.Assunto).NotEmpty().WithMessage("Informe o assunto.").Length(3, 200);
        RuleFor(x => x.Corpo).NotEmpty().WithMessage("Informe o texto da mensagem.").MaximumLength(10000);
    }
}

public sealed class CriarRascunhoHandler(IValidator<CriarRascunhoCommand> validador, IMensagemRepository mensagens,
    IAlunoRepository alunos, IEncaminhamentoRepository encaminhamentos, ICursoQueries cursos, IUsuarioQueries usuarios,
    IUsuarioAtual usuario, IRelogio relogio, IUnidadeDeTrabalho uow)
{
    public async Task<Guid> Handle(CriarRascunhoCommand comando, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);
        var v = await RegrasComunicacao.ValidarVinculosAsync(usuario, comando.CursoId, comando.AlunoId, comando.EncaminhamentoId,
            alunos, encaminhamentos, cursos, ct);
        await RegrasComunicacao.ValidarDestinatarioDoRascunhoAsync(usuario, comando.DestinatarioId, usuarios, ct);

        var m = Mensagem.CriarRascunho(usuario.Id, comando.Tipo, comando.Assunto, comando.Corpo, comando.DestinatarioId,
            v.CursoId, v.AlunoId, v.EncaminhamentoId, relogio.Agora);
        mensagens.Adicionar(m);
        await uow.SalvarAsync(ct);
        return m.Id;
    }
}
