using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Documentos.Commands;

public record ArquivarDocumentoCommand(Guid Id, string Motivo, string Versao);

public class ArquivarDocumentoValidator : AbstractValidator<ArquivarDocumentoCommand>
{
    public ArquivarDocumentoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Motivo).Must(t => t?.Trim().Length is >= 3 and <= 500)
            .WithMessage("O motivo deve ter de 3 a 500 caracteres.");
    }
}

public sealed class ArquivarDocumentoHandler(IValidator<ArquivarDocumentoCommand> validador,
    IDocumentoRepository documentos, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task Handle(ArquivarDocumentoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        var escopo = EscopoDocumentos.Para(usuario, incluirArquivados: true);

        var d = await documentos.ObterAsync(comando.Id, ct);
        if (d is null || !escopo.PodeVer(d)) throw new NotFoundException("Documento");
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();
        uow.ExigirVersao(d, comando.Versao);

        d.Arquivar(comando.Motivo, usuario.Id, relogio.Agora);
        auditoria.Registrar("documento.arquivar", "Documento", d.Id);
        await uow.SalvarAsync(ct);
    }
}
