using Nap.Application.Features.Documentos;
using Nap.Domain.Documentos;

namespace Nap.Application.Tests.Fakes;

public sealed class DocumentoRepositoryEmMemoria : IDocumentoRepository
{
    public List<Documento> Documentos { get; } = [];

    public Task<Documento?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Documentos.FirstOrDefault(d => d.Id == id));

    public void Adicionar(Documento d) => Documentos.Add(d);

    /// <summary>Simula commit com a resposta perdida: o "banco" confirma o que foi adicionado mesmo que o SaveChanges tenha lançado.</summary>
    public bool CommitApesarDaFalha { get; set; }

    public Task<bool> ExisteAsync(Guid id, CancellationToken ct) => Task.FromResult(CommitApesarDaFalha && Documentos.Any(d => d.Id == id));
}
