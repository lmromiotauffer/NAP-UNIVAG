using Nap.Application.Features.Documentos;
using Nap.Domain.Documentos;

namespace Nap.Application.Tests.Fakes;

public sealed class DocumentoRepositoryEmMemoria : IDocumentoRepository
{
    public List<Documento> Documentos { get; } = [];

    public Task<Documento?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Documentos.FirstOrDefault(d => d.Id == id));

    public void Adicionar(Documento d) => Documentos.Add(d);
}
