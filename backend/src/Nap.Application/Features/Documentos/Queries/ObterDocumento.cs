using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Documentos.Queries;

/// <summary><c>IncluirArquivados</c> só vale para Administrador/CoordenadorNap.</summary>
public record ObterDocumentoQuery(Guid Id, bool IncluirArquivados = false);

public class ObterDocumentoValidator : AbstractValidator<ObterDocumentoQuery>
{
    public ObterDocumentoValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ObterDocumentoHandler(IValidator<ObterDocumentoQuery> validador, IDocumentoQueries consultas,
    IUsuarioAtual usuario)
{
    public async Task<DocumentoDto> Handle(ObterDocumentoQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        var escopo = EscopoDocumentos.Para(usuario, query.IncluirArquivados);
        return await consultas.ObterAsync(query.Id, escopo, ct) ?? throw new NotFoundException("Documento");
    }
}
