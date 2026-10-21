using Nap.Application.Comum;
using Nap.Application.Features.Documentos;

namespace Nap.Application.Tests.Fakes;

/// <summary>Aplica <see cref="EscopoDocumentos.PodeVer"/> sobre os documentos do repositório em memória.</summary>
public sealed class DocumentoQueriesFalso(DocumentoRepositoryEmMemoria repo) : IDocumentoQueries
{
    public Task<PagedResult<DocumentoDto>?> ListarAsync(Guid alunoId, EscopoDocumentos e, int pagina, int tamanho, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<DocumentoDto?> ObterAsync(Guid id, EscopoDocumentos e, CancellationToken ct) => throw new NotSupportedException();

    public Task<ArquivoDocumento?> ObterArquivoAsync(Guid id, EscopoDocumentos e, CancellationToken ct) =>
        Task.FromResult(repo.Documentos.FirstOrDefault(d => d.Id == id && e.PodeVer(d)) is { } d
            ? new ArquivoDocumento(d.Id, d.ChaveArmazenamento, d.NomeArquivo, d.TipoConteudo) : null);
}
