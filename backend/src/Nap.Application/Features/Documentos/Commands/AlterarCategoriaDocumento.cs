using FluentValidation;
using Nap.Application.Comum;
using Nap.Domain.Documentos;

namespace Nap.Application.Features.Documentos.Commands;

public record AlterarCategoriaDocumentoCommand(Guid Id, CategoriaDocumento Categoria, string Versao);

public class AlterarCategoriaDocumentoValidator : AbstractValidator<AlterarCategoriaDocumentoCommand>
{
    public AlterarCategoriaDocumentoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Categoria).IsInEnum();
    }
}

public sealed class AlterarCategoriaDocumentoHandler(IValidator<AlterarCategoriaDocumentoCommand> validador,
    IDocumentoRepository documentos, IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task Handle(AlterarCategoriaDocumentoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        var escopo = EscopoDocumentos.Para(usuario, incluirArquivados: true);

        var d = await documentos.ObterAsync(comando.Id, ct);
        if (d is null || !escopo.PodeVer(d)) throw new NotFoundException("Documento");
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();
        uow.ExigirVersao(d, comando.Versao);

        d.AlterarCategoria(comando.Categoria);
        auditoria.Registrar("documento.categoria", "Documento", d.Id);
        await uow.SalvarAsync(ct);
    }
}
