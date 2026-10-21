using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Documentos.Commands;

public record RevogarCompartilhamentoDocumentoCommand(Guid Id, string Justificativa, string Versao);

public class RevogarCompartilhamentoDocumentoValidator : AbstractValidator<RevogarCompartilhamentoDocumentoCommand>
{
    public RevogarCompartilhamentoDocumentoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Justificativa).Must(t => t?.Trim().Length is >= 3 and <= 500)
            .WithMessage("A justificativa deve ter de 3 a 500 caracteres.");
    }
}

public sealed class RevogarCompartilhamentoDocumentoHandler(IValidator<RevogarCompartilhamentoDocumentoCommand> validador,
    IDocumentoRepository documentos, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task Handle(RevogarCompartilhamentoDocumentoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        var escopo = EscopoDocumentos.Para(usuario, incluirArquivados: true);

        var d = await documentos.ObterAsync(comando.Id, ct);
        if (d is null || !escopo.PodeVer(d)) throw new NotFoundException("Documento");
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();
        uow.ExigirVersao(d, comando.Versao);

        d.RevogarCompartilhamento(comando.Justificativa, usuario.Id, relogio.Agora);
        auditoria.Registrar("documento.revogar", "Documento", d.Id);
        await uow.SalvarAsync(ct);
    }
}
