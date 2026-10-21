using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Notificacoes;
using Nap.Domain.Notificacoes;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class NotificacaoRepository(NapDbContext db) : INotificacaoRepository
{
    public async Task<IReadOnlyList<Guid>> DestinatariosJaNotificadosAsync(Guid eventoId, CancellationToken ct) =>
        await db.Notificacoes.Where(n => n.EventoId == eventoId).Select(n => n.DestinatarioId).ToListAsync(ct);

    public Task<Notificacao?> ObterAsync(Guid id, CancellationToken ct) => db.Notificacoes.SingleOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<Notificacao>> ListarNaoLidasAsync(Guid destinatarioId, string tipo, string link,
        CancellationToken ct) =>
        await db.Notificacoes.Where(n => n.DestinatarioId == destinatarioId && n.Tipo == tipo && n.Link == link && n.LidaEm == null)
            .ToListAsync(ct);

    public Task MarcarTodasLidasAsync(Guid destinatarioId, DateTimeOffset agora, CancellationToken ct) =>
        db.Notificacoes.Where(n => n.DestinatarioId == destinatarioId && n.LidaEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.LidaEm, agora), ct);

    public void Adicionar(Notificacao n) => db.Notificacoes.Add(n);
}
