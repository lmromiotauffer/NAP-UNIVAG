using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Documentos;

namespace Nap.Application.Features.Peis.Queries;

public record BaixarPdfPeiQuery(Guid VersaoId);

public class BaixarPdfPeiValidator : AbstractValidator<BaixarPdfPeiQuery>
{
    public BaixarPdfPeiValidator() => RuleFor(x => x.VersaoId).NotEmpty();
}

public sealed class BaixarPdfPeiHandler(IValidator<BaixarPdfPeiQuery> validador, IPeiQueries consultas, IGeradorPdfPei gerador,
    IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    /// <returns>O chamador descarta <see cref="ArquivoParaDownload.Conteudo"/>.</returns>
    public async Task<ArquivoParaDownload> Handle(BaixarPdfPeiQuery query, CancellationToken ct = default)
    {
        var escopo = EscopoPeis.Para(usuario);
        await validador.ValidarAsync(query, ct);
        var detalhe = await consultas.ObterVersaoAsync(query.VersaoId, escopo, usuario, ct)
            ?? throw new NotFoundException("Versão do PEI");
        var pdf = gerador.Gerar(ModeloPdfPei.Montar(detalhe, escopo.Todos ? VariantePdf.Completa : VariantePdf.Pedagogica));
        auditoria.Registrar("pei.pdf", "VersaoPei", detalhe.Id);
        await uow.SalvarAsync(ct);
        return new ArquivoParaDownload(new MemoryStream(pdf, writable: false), $"PEI-{detalhe.Matricula}-v{detalhe.Numero}.pdf",
            "application/pdf");
    }
}
