using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Auditoria;

namespace Nap.Infrastructure.Persistencia.Consultas;

public sealed class AuditoriaQueries(NapDbContext db, IRelogio relogio) : IAuditoriaQueries
{
    public async Task<PagedResult<EventoAuditoriaDto>> ListarAsync(FiltroAuditoria f, CancellationToken ct)
    {
        var de = relogio.Fuso.InicioDoDia(f.Inicio);
        var ate = relogio.Fuso.InicioDoDia(f.Fim.AddDays(1));
        var q = db.EventosAuditoria.AsNoTracking().Where(e => e.Em >= de && e.Em < ate);
        if (f.UsuarioId is { } usuario) q = q.Where(e => e.UsuarioId == usuario);
        // Prefixo: "pei." traz "pei.criar", não "peis.x".
        if (!string.IsNullOrEmpty(f.Acao)) q = q.Where(e => e.Acao.StartsWith(f.Acao));
        if (!string.IsNullOrEmpty(f.Recurso)) q = q.Where(e => e.Recurso == f.Recurso);
        if (f.RecursoId is { } recursoId) q = q.Where(e => e.RecursoId == recursoId);
        if (!string.IsNullOrEmpty(f.Resultado)) q = q.Where(e => e.Resultado == f.Resultado);

        var total = await q.CountAsync(ct);
        var itens = await q
            .OrderByDescending(e => e.Em).ThenByDescending(e => e.Id)
            .Skip((f.Pagina - 1) * f.Tamanho).Take(f.Tamanho)
            .Select(e => new EventoAuditoriaDto(e.Id, e.Em, e.UsuarioId,
                db.Users.Where(u => u.Id == e.UsuarioId).Select(u => u.NomeCompleto).FirstOrDefault(),
                e.Acao, e.Recurso, e.RecursoId, e.Resultado))
            .ToListAsync(ct);
        return new PagedResult<EventoAuditoriaDto>(itens, f.Pagina, f.Tamanho, total);
    }
}
