using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Comunicacoes;
using Nap.Domain.Comunicacoes;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class MensagemRepository(NapDbContext db) : IMensagemRepository
{
    public Task<Mensagem?> ObterAsync(Guid id, CancellationToken ct) => db.Mensagens.SingleOrDefaultAsync(m => m.Id == id, ct);

    public void Adicionar(Mensagem m) => db.Mensagens.Add(m);

    public void Remover(Mensagem m) => db.Mensagens.Remove(m);

    public async Task<bool> RecarregarAsync(Mensagem m, CancellationToken ct)
    {
        var entrada = db.Entry(m);
        await entrada.ReloadAsync(ct);
        return entrada.State != EntityState.Detached;
    }
}
