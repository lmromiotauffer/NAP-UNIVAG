using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Notificacoes;

namespace Nap.Infrastructure.Persistencia.Consultas;

public sealed class NotificacaoQueries(NapDbContext db) : INotificacaoQueries
{
    public async Task<PagedResult<NotificacaoDto>> ListarAsync(Guid destinatarioId, bool naoLidas, int pagina, int tamanho,
        CancellationToken ct)
    {
        var q = db.Notificacoes.AsNoTracking().Where(n => n.DestinatarioId == destinatarioId && (!naoLidas || n.LidaEm == null));
        var total = await q.CountAsync(ct);
        var itens = await q.OrderByDescending(n => n.CriadaEm).ThenByDescending(n => n.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(n => new NotificacaoDto(n.Id, n.Tipo, n.Titulo, n.Texto, n.Link, n.CriadaEm, n.LidaEm != null))
            .ToListAsync(ct);
        return new PagedResult<NotificacaoDto>(itens, pagina, tamanho, total);
    }

    public Task<int> ContarNaoLidasAsync(Guid destinatarioId, CancellationToken ct) =>
        db.Notificacoes.CountAsync(n => n.DestinatarioId == destinatarioId && n.LidaEm == null, ct);
}
