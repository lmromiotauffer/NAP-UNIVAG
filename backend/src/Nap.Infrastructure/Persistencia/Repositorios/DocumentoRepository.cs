using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Documentos;
using Nap.Domain.Documentos;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class DocumentoRepository(NapDbContext db) : IDocumentoRepository
{
    public Task<Documento?> ObterAsync(Guid id, CancellationToken ct) => db.Documentos.SingleOrDefaultAsync(d => d.Id == id, ct);

    public void Adicionar(Documento d) => db.Documentos.Add(d);

    public Task<bool> ExisteAsync(Guid id, CancellationToken ct) => db.Documentos.AsNoTracking().AnyAsync(d => d.Id == id, ct);
}
