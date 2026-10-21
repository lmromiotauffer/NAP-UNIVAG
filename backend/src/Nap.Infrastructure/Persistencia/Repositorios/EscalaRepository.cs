using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Domain.Agenda;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class EscalaRepository(NapDbContext db) : IEscalaRepository
{
    public Task<EscalaProfissional?> ObterAsync(Guid id, CancellationToken ct) =>
        db.EscalasProfissional.SingleOrDefaultAsync(e => e.Id == id, ct);

    // Vigências não se sobrepõem; o First só evita derrubar a agenda se um dia houver duas.
    public Task<EscalaProfissional?> ObterVigenteAsync(Guid profissionalId, DateOnly data, CancellationToken ct) =>
        db.EscalasProfissional.AsNoTracking()
            .Where(e => e.ProfissionalId == profissionalId && e.VigenciaInicio <= data && (e.VigenciaFim == null || e.VigenciaFim >= data))
            .OrderByDescending(e => e.VigenciaInicio)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<EscalaProfissional>> ListarDoProfissionalAsync(Guid profissionalId, CancellationToken ct) =>
        await db.EscalasProfissional.Where(e => e.ProfissionalId == profissionalId)
            .OrderByDescending(e => e.VigenciaInicio).ToListAsync(ct);

    public async Task<IReadOnlyList<EscalaProfissionalDto>> ListarAsync(Guid? profissionalId, CancellationToken ct)
    {
        var q = db.EscalasProfissional.AsNoTracking();
        if (profissionalId is { } p) q = q.Where(e => e.ProfissionalId == p);
        var linhas = await q
            .Select(e => new
            {
                e,
                Versao = EF.Property<byte[]>(e, "Versao"),
                Profissional = db.Users.Where(u => u.Id == e.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
            })
            .OrderBy(x => x.Profissional).ThenBy(x => x.e.ProfissionalId).ThenByDescending(x => x.e.VigenciaInicio)
            .ToListAsync(ct);
        return linhas.Select(x => new EscalaProfissionalDto(x.e.Id, x.e.ProfissionalId, x.Profissional, x.e.VigenciaInicio,
                x.e.VigenciaFim, x.e.Faixas.OrderBy(f => (int)f.Dia).Select(f => new FaixaEscalaDto(f.Dia, f.Inicio, f.Fim, f.SemAtendimento)).ToList(),
                Convert.ToBase64String(x.Versao)))
            .ToList();
    }

    public void Adicionar(EscalaProfissional e) => db.EscalasProfissional.Add(e);

    public Task<AutorizacaoForaEscala?> ObterAutorizacaoAsync(Guid id, CancellationToken ct) =>
        db.AutorizacoesForaEscala.SingleOrDefaultAsync(a => a.Id == id, ct);

    public Task<bool> ExisteAutorizacaoAtivaAsync(Guid profissionalId, DateTimeOffset inicio, CancellationToken ct) =>
        db.AutorizacoesForaEscala.AsNoTracking().AnyAsync(a => a.ProfissionalId == profissionalId && a.RevogadaEm == null
            && a.Intervalo.Inicio <= inicio && inicio < a.Intervalo.Fim, ct);

    public async Task<PagedResult<AutorizacaoForaEscalaDto>> ListarAutorizacoesAsync(Guid? profissionalId, DateTimeOffset? de,
        DateTimeOffset? ate, int pagina, int tamanho, CancellationToken ct)
    {
        var q = db.AutorizacoesForaEscala.AsNoTracking().AsQueryable();
        if (profissionalId is { } p) q = q.Where(a => a.ProfissionalId == p);
        if (de is { } d) q = q.Where(a => a.Intervalo.Fim > d);
        if (ate is { } t) q = q.Where(a => a.Intervalo.Inicio < t);
        var total = await q.CountAsync(ct);
        var itens = await q.OrderByDescending(a => a.Intervalo.Inicio).ThenBy(a => a.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(a => new AutorizacaoForaEscalaDto(a.Id, a.ProfissionalId,
                db.Users.Where(u => u.Id == a.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                a.Intervalo.Inicio, a.Intervalo.Fim, a.Justificativa, a.AutorizadoPorId,
                db.Users.Where(u => u.Id == a.AutorizadoPorId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                a.CriadaEm, a.RevogadaEm))
            .ToListAsync(ct);
        return new PagedResult<AutorizacaoForaEscalaDto>(itens, pagina, tamanho, total);
    }

    public void AdicionarAutorizacao(AutorizacaoForaEscala a) => db.AutorizacoesForaEscala.Add(a);
}
