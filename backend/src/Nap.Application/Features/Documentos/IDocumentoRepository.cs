using Nap.Domain.Documentos;

namespace Nap.Application.Features.Documentos;

public interface IDocumentoRepository
{
    Task<Documento?> ObterAsync(Guid id, CancellationToken ct);
    void Adicionar(Documento d);

    /// <summary>Lido do banco, nunca do rastreamento: diz se o documento chegou a ser gravado.</summary>
    Task<bool> ExisteAsync(Guid id, CancellationToken ct);
}
