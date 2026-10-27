using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum;

namespace Nap.Infrastructure.Persistencia.Consultas;

public sealed class UsuarioQueries(NapDbContext db) : IUsuarioQueries
{
    // Array: o EF só traduz Contains sobre coleção simples.
    private static readonly string[] PerfisProfissionais = [.. Perfis.EquipeNap];

    public Task<bool> EhProfissionalAtivoAsync(Guid id, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Id == id && u.Ativo && PerfisProfissionais.Contains(u.Perfil), ct);

    public async Task<IReadOnlyList<ProfissionalDto>> ListarProfissionaisAsync(bool incluirInativos, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => (incluirInativos || u.Ativo) && PerfisProfissionais.Contains(u.Perfil))
            .OrderBy(u => u.NomeCompleto).ThenBy(u => u.Id)
            .Select(u => new ProfissionalDto(u.Id, u.NomeCompleto, u.Ativo))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProfissionalProducaoDto>> ListarProfissionaisDaProducaoAsync(DateOnly inicio, DateOnly fim,
        TimeZoneInfo fuso, Guid? profissionalId, CancellationToken ct)
    {
        var de = fuso.InicioDoDia(inicio);
        var ate = fuso.InicioDoDia(fim.AddDays(1));
        var primeiraSemana = ServicoFechamento.SegundaDe(inicio);
        // O perfil atual só filtra quem não tem fatos: quem saiu da equipe NAP mantém a produção passada.
        // shortcut: sem histórico de perfil, quem saiu da equipe segue com capacidade pela escala; registrar a data da troca se pesar.
        return await db.Users.AsNoTracking()
            .Where(u => profissionalId == null || u.Id == profissionalId)
            .Where(u => (PerfisProfissionais.Contains(u.Perfil) && (profissionalId != null || u.Ativo))
                    || db.Atendimentos.Any(a => a.ProfissionalId == u.Id && a.Intervalo.Inicio >= de && a.Intervalo.Inicio < ate
                        && (a.Estado == EstadoAtendimento.Realizado || a.Estado == EstadoAtendimento.Ausente))
                    || db.AtividadesCurso.Any(a => a.ProfissionalId == u.Id && a.Intervalo.Inicio >= de && a.Intervalo.Inicio < ate)
                    || db.Compromissos.Any(c => c.ProfissionalId == u.Id && c.RealizacaoInicio >= de && c.RealizacaoInicio < ate)
                    || db.AjustesProducao.Any(a => a.ProfissionalId == u.Id && a.Data >= inicio && a.Data <= fim)
                    || db.FechamentosSemanais.Any(f => f.ProfissionalId == u.Id && f.Semana >= primeiraSemana && f.Semana <= fim))
            .OrderBy(u => u.NomeCompleto).ThenBy(u => u.Id)
            .Select(u => new ProfissionalProducaoDto(u.Id, u.NomeCompleto, u.Ativo, u.DesativadoEm, u.Perfil, u.AtivoDesde, u.ReativadoEm))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListarAtivosPorPerfilAsync(string perfil, Guid? cursoId, CancellationToken ct) =>
        await db.Users.Where(u => u.Ativo && u.Perfil == perfil && (cursoId == null || u.CursoId == cursoId))
            .Select(u => u.Id).ToListAsync(ct);

    public Task<bool> EstaAtivoAsync(Guid id, CancellationToken ct) => db.Users.AnyAsync(u => u.Id == id && u.Ativo, ct);

    public Task<UsuarioResumoDto?> ObterResumoAsync(Guid id, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new UsuarioResumoDto(u.Id, u.NomeCompleto, u.Perfil, u.CursoId, u.Ativo)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<UsuarioResumoDto>> ListarAtivosAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Ativo)
            .OrderBy(u => u.NomeCompleto).ThenBy(u => u.Id)
            .Select(u => new UsuarioResumoDto(u.Id, u.NomeCompleto, u.Perfil, u.CursoId, u.Ativo))
            .ToListAsync(ct);
}
