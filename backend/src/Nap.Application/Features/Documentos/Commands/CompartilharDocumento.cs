using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Alunos;

namespace Nap.Application.Features.Documentos.Commands;

public record CompartilharDocumentoCommand(Guid Id, string Justificativa, string Versao);

public class CompartilharDocumentoValidator : AbstractValidator<CompartilharDocumentoCommand>
{
    public CompartilharDocumentoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Justificativa).Must(t => t?.Trim().Length is >= 3 and <= 500)
            .WithMessage("A justificativa deve ter de 3 a 500 caracteres.");
    }
}

public sealed class CompartilharDocumentoHandler(IValidator<CompartilharDocumentoCommand> validador,
    IDocumentoRepository documentos, IAlunoRepository alunos, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task Handle(CompartilharDocumentoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        var escopo = EscopoDocumentos.Para(usuario, incluirArquivados: true);

        var d = await documentos.ObterAsync(comando.Id, ct);
        if (d is null || !escopo.PodeVer(d)) throw new NotFoundException("Documento");
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();
        uow.ExigirVersao(d, comando.Versao);

        var aluno = await alunos.ObterAsync(d.AlunoId, ct) ?? throw new NotFoundException("Documento");
        d.Compartilhar(comando.Justificativa, usuario.Id, aluno.CursoAtualId, relogio.Agora);
        auditoria.Registrar("documento.compartilhar", "Documento", d.Id);
        await uow.SalvarAsync(ct);
    }
}
