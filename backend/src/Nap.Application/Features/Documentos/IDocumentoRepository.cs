using Nap.Domain.Documentos;

namespace Nap.Application.Features.Documentos;

public interface IDocumentoRepository
{
    Task<Documento?> ObterAsync(Guid id, CancellationToken ct);
    void Adicionar(Documento d);
}
