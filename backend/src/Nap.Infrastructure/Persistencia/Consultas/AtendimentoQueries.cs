using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Atendimentos;

namespace Nap.Infrastructure.Persistencia.Consultas;

public sealed class AtendimentoQueries(NapDbContext db, IRelogio relogio) : IAtendimentoQueries
{
    public async Task<PagedResult<AtendimentoListaDto>> ListarAsync(FiltroAtendimentos f, CancellationToken ct)
    {
        var q = db.Atendimentos.AsNoTracking();
        if (f.AlunoId is { } aluno) q = q.Where(a => a.AlunoId == aluno);
        if (f.ProfissionalId is { } prof) q = q.Where(a => a.ProfissionalId == prof);
        if (f.Estado is { } estado) q = q.Where(a => a.Estado == estado);
        if (f.Tipo is { } tipo) q = q.Where(a => a.Tipo == tipo);
        if (f.De is { } de)
        {
            var inicio = relogio.Fuso.InicioDoDia(de);
            q = q.Where(a => a.Intervalo.Inicio >= inicio);
        }
        if (f.Ate is { } ate)
        {
            var fim = relogio.Fuso.InicioDoDia(ate.AddDays(1));
            q = q.Where(a => a.Intervalo.Inicio < fim);
        }

        var total = await q.CountAsync(ct);
        // Lista nunca traz observações privadas.
        var itens = await q
            .OrderByDescending(a => a.Intervalo.Inicio).ThenByDescending(a => a.Id)
            .Skip((f.Pagina - 1) * f.Tamanho).Take(f.Tamanho)
            .Select(a => new AtendimentoListaDto(a.Id, a.Intervalo.Inicio,
                EF.Functions.DateDiffMinute(a.Intervalo.Inicio, a.Intervalo.Fim),
                a.AlunoId, db.Alunos.Where(x => x.Id == a.AlunoId).Select(x => x.NomeCompleto).FirstOrDefault() ?? "",
                a.Tipo, a.Dados == null ? null : a.Dados.DemandaPrincipal, a.ProfissionalId,
                db.Users.Where(u => u.Id == a.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                a.Estado, a.CompromissoId == null))
            .ToListAsync(ct);
        return new PagedResult<AtendimentoListaDto>(itens, f.Pagina, f.Tamanho, total);
    }

    public Task<AtendimentoDetalheDto?> ObterAsync(Guid id, CancellationToken ct) =>
        db.Atendimentos.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AtendimentoDetalheDto(a.Id, a.CompromissoId, a.Intervalo.Inicio, a.Intervalo.Fim,
                EF.Functions.DateDiffMinute(a.Intervalo.Inicio, a.Intervalo.Fim),
                a.AlunoId, db.Alunos.Where(x => x.Id == a.AlunoId).Select(x => x.NomeCompleto).FirstOrDefault() ?? "",
                a.ProfissionalId, db.Users.Where(u => u.Id == a.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                a.Tipo, a.Estado, a.CompromissoId == null, a.JustificativaAvulso,
                a.Dados == null ? null : new DadosClinicosDto(a.Dados.DemandaPrincipal, a.Dados.ObservacoesPrivadas,
                    a.Dados.RecomendacoesEncaminhamento, a.Dados.ProximosPassos),
                a.Correcoes.OrderBy(c => c.Em).ThenBy(c => c.Id)
                    .Select(c => new CorrecaoAtendimentoDto(
                        db.Users.Where(u => u.Id == c.AutorId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                        c.Em, c.Justificativa, c.EstadoAnterior, c.EstadoNovo, c.TipoAnterior, c.InicioAnterior, c.FimAnterior))
                    .ToList(),
                Convert.ToBase64String(EF.Property<byte[]>(a, "Versao"))))
            .SingleOrDefaultAsync(ct);
}
