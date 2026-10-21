using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Documentos.Queries;

/// <summary><c>IncluirArquivados</c> só vale para Administrador/CoordenadorNap (arquivado só baixa com ele).</summary>
public record BaixarDocumentoQuery(Guid Id, bool IncluirArquivados = false);

public class BaixarDocumentoValidator : AbstractValidator<BaixarDocumentoQuery>
{
    public BaixarDocumentoValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class BaixarDocumentoHandler(IValidator<BaixarDocumentoQuery> validador, IDocumentoQueries consultas,
    IArmazenamentoArquivos armazenamento, IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    /// <returns>O chamador descarta <see cref="ArquivoParaDownload.Conteudo"/>.</returns>
    public async Task<ArquivoParaDownload> Handle(BaixarDocumentoQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        var escopo = EscopoDocumentos.Para(usuario, query.IncluirArquivados);
        var arquivo = await consultas.ObterArquivoAsync(query.Id, escopo, ct) ?? throw new NotFoundException("Documento");

        // Registra a tentativa mesmo que o arquivo falte no armazenamento (AbrirAsync lança 404 depois).
        auditoria.Registrar("documento.download", "Documento", arquivo.Id);
        await uow.SalvarAsync(ct);
        // Aberto só depois de salvar: uma falha no banco não deixa stream aberto para trás.
        var conteudo = await armazenamento.AbrirAsync(arquivo.Chave, ct);
        return new ArquivoParaDownload(conteudo, arquivo.NomeArquivo, arquivo.TipoConteudo);
    }
}
