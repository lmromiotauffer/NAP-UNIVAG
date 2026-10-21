using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class FeriadoRepository(NapDbContext db, IRelogio relogio) : IFeriadoRepository
{
    public Task<Feriado?> ObterAsync(Guid id, CancellationToken ct) => db.Feriados.SingleOrDefaultAsync(f => f.Id == id, ct);

    public Task<Feriado?> ObterAtivoAsync(DateOnly data, CancellationToken ct) =>
        db.Feriados.AsNoTracking().SingleOrDefaultAsync(f => f.Data == data && f.Ativo, ct);

    public async Task<IReadOnlyList<Feriado>> ListarAtivosAsync(DateOnly de, DateOnly ate, CancellationToken ct) =>
        await db.Feriados.AsNoTracking().Where(f => f.Ativo && f.Data >= de && f.Data <= ate)
            .OrderBy(f => f.Data).ToListAsync(ct);

    public async Task<IReadOnlyList<ConflitoFeriadoDto>> ListarConflitosAsync(DateOnly data, CancellationToken ct)
    {
        var inicio = relogio.Fuso.InicioDoDia(data);
        var fim = relogio.Fuso.InicioDoDia(data.AddDays(1));
        var compromissos = await db.Compromissos.AsNoTracking()
            .Where(c => c.Estado == EstadoCompromisso.Agendado && c.Tipo != TipoCompromisso.Lembrete
                && c.Intervalo.Inicio < fim && c.Intervalo.Fim > inicio)
            .Select(c => new ConflitoFeriadoDto(c.Id, "Compromisso", c.Titulo, c.ProfissionalId,
                db.Users.Where(u => u.Id == c.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                c.Intervalo.Inicio, c.Intervalo.Fim))
            .ToListAsync(ct);
        // Sem dados clínicos: só o fato de haver atendimento registrado no horário.
        var atendimentos = await db.Atendimentos.AsNoTracking()
            .Where(a => (a.Estado == EstadoAtendimento.Realizado || a.Estado == EstadoAtendimento.Ausente)
                && a.Intervalo.Inicio < fim && a.Intervalo.Fim > inicio)
            .Select(a => new ConflitoFeriadoDto(a.Id, "Atendimento", "Atendimento", a.ProfissionalId,
                db.Users.Where(u => u.Id == a.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                a.Intervalo.Inicio, a.Intervalo.Fim))
            .ToListAsync(ct);
        return compromissos.Concat(atendimentos).OrderBy(c => c.Inicio).ThenBy(c => c.Id).ToList();
    }

    public void Adicionar(Feriado f) => db.Feriados.Add(f);
}
