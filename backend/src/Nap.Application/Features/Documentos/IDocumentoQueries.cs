using Nap.Application.Comum;

namespace Nap.Application.Features.Documentos;

/// <summary>Localização do arquivo de um documento visível; nunca sai da aplicação.</summary>
public record ArquivoDocumento(Guid Id, string Chave, string NomeArquivo, string TipoConteudo);

public interface IDocumentoQueries
{
    /// <summary><c>null</c> quando o aluno não existe ou está fora do escopo (coordenador: aluno do curso atual dele).</summary>
    Task<PagedResult<DocumentoDto>?> ListarAsync(Guid alunoId, EscopoDocumentos e, int pagina, int tamanho, CancellationToken ct);

    /// <summary><c>null</c> quando não existe ou está fora do escopo.</summary>
    Task<DocumentoDto?> ObterAsync(Guid id, EscopoDocumentos e, CancellationToken ct);

    /// <summary><c>null</c> quando não existe ou está fora do escopo.</summary>
    Task<ArquivoDocumento?> ObterArquivoAsync(Guid id, EscopoDocumentos e, CancellationToken ct);
}
