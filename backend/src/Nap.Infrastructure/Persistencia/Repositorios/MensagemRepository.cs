using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Comunicacoes;
using Nap.Domain.Comunicacoes;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class MensagemRepository(NapDbContext db) : IMensagemRepository
{
    public Task<Mensagem?> ObterAsync(Guid id, CancellationToken ct) => db.Mensagens.SingleOrDefaultAsync(m => m.Id == id, ct);

    public void Adicionar(Mensagem m) => db.Mensagens.Add(m);

    public void Remover(Mensagem m) => db.Mensagens.Remove(m);
}
