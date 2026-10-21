using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Producao;

namespace Nap.Infrastructure.Persistencia.Consultas;

public sealed class ProducaoQueries(NapDbContext db, IRelogio relogio) : IProducaoQueries
{
    public Task<int> ContarPendentesAsync(Guid profissionalId, DateOnly semana, CancellationToken ct)
    {
        var de = relogio.Fuso.InicioDoDia(semana);
        var ate = relogio.Fuso.InicioDoDia(semana.AddDays(7));
        return PainelQueries.SemDesfecho(db, relogio, profissionalId, null)
            .CountAsync(c => c.Intervalo.Inicio >= de && c.Intervalo.Inicio < ate, ct);
    }

    public async Task<IReadOnlyList<SemanaProducaoDto>> ListarSemanasAsync(IReadOnlyList<ProfissionalDto> profissionais, DateOnly inicio,
        DateOnly fim, CancellationToken ct)
    {
        if (profissionais.Count == 0) return [];
        var ids = profissionais.Select(p => p.Id).ToList();
        var primeira = ServicoFechamento.SegundaDe(inicio);
        var de = relogio.Fuso.InicioDoDia(primeira);
        var ate = relogio.Fuso.InicioDoDia(ServicoFechamento.SegundaDe(fim).AddDays(7));

        var pendentes = (await PainelQueries.SemDesfecho(db, relogio, null, null)
                .Where(c => ids.Contains(c.ProfissionalId) && c.Intervalo.Inicio >= de && c.Intervalo.Inicio < ate)
                .Select(c => new { c.ProfissionalId, c.Intervalo.Inicio })
                .ToListAsync(ct))
            .GroupBy(c => (c.ProfissionalId, ServicoFechamento.SegundaDe(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(c.Inicio, relogio.Fuso).DateTime))))
            .ToDictionary(g => g.Key, g => g.Count());

        var fechamentos = (await db.FechamentosSemanais.AsNoTracking()
                .Where(f => ids.Contains(f.ProfissionalId) && f.Semana >= primeira && f.Semana <= fim)
                .Select(f => new
                {
                    f.ProfissionalId, f.Semana, f.Fechada, Versao = EF.Property<byte[]>(f, "Versao"),
                    Eventos = f.Eventos.OrderBy(e => e.Em).Select(e => new
                    {
                        e.Tipo, Autor = db.Users.Where(u => u.Id == e.AutorId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                        e.Em, e.Justificativa,
                    }).ToList(),
                })
                .ToListAsync(ct))
            .ToDictionary(f => (f.ProfissionalId, f.Semana));

        var semanas = new List<SemanaProducaoDto>();
        foreach (var p in profissionais)
            for (var s = primeira; s <= fim; s = s.AddDays(7))
            {
                fechamentos.TryGetValue((p.Id, s), out var f);
                semanas.Add(new SemanaProducaoDto(p.Id, p.Nome, s, f?.Fechada ?? false, pendentes.GetValueOrDefault((p.Id, s)),
                    f is null ? null : Convert.ToBase64String(f.Versao),
                    f?.Eventos.Select(e => new EventoFechamentoDto(e.Tipo, e.Autor, e.Em, e.Justificativa)).ToList() ?? []));
            }
        return semanas;
    }

    // Uma consulta por tipo de fato para o período inteiro; nada por dia.
    public async Task<FatosProducao> CarregarFatosAsync(IReadOnlyList<Guid> profissionais, DateOnly inicio, DateOnly fim, Guid? cursoId,
        CancellationToken ct)
    {
        var ids = profissionais.ToList();
        var de = relogio.Fuso.InicioDoDia(inicio);
        var ate = relogio.Fuso.InicioDoDia(fim.AddDays(1));

        var atendimentos = (await PainelQueries.Atendimentos(db, relogio, de, ate, null, cursoId)
                .Where(a => ids.Contains(a.ProfissionalId)
                    && (a.Estado == EstadoAtendimento.Realizado || a.Estado == EstadoAtendimento.Ausente))
                .Select(a => new { a.ProfissionalId, a.Estado, a.Tipo, a.Intervalo.Inicio, a.Intervalo.Fim })
                .ToListAsync(ct))
            .Select(a => new OcorrenciaProfissional(a.ProfissionalId,
                a.Estado == EstadoAtendimento.Realizado ? CategoriaProducao.AtendimentosRealizados : CategoriaProducao.Ausencias,
                a.Inicio, a.Fim, a.Tipo != TipoAtendimento.Reuniao));
        // Atividades só pelos registros (nunca pelo compromisso concluído); institucionais (sem curso) ficam fora do filtro de curso.
        var atividades = (await db.AtividadesCurso.AsNoTracking()
                .Where(a => ids.Contains(a.ProfissionalId) && a.Intervalo.Inicio >= de && a.Intervalo.Inicio < ate
                    && (cursoId == null || a.CursoId == cursoId))
                .Select(a => new { a.ProfissionalId, a.Intervalo.Inicio, a.Intervalo.Fim })
                .ToListAsync(ct))
            .Select(a => new OcorrenciaProfissional(a.ProfissionalId, CategoriaProducao.AtividadesCursos, a.Inicio, a.Fim, false));
        var pendentes = (await PainelQueries.SemDesfecho(db, relogio, null, cursoId)
                .Where(c => ids.Contains(c.ProfissionalId) && c.Intervalo.Inicio >= de && c.Intervalo.Inicio < ate)
                .Select(c => new { c.ProfissionalId, c.Intervalo.Inicio, c.Intervalo.Fim })
                .ToListAsync(ct))
            .Select(c => new IntervaloProfissional(c.ProfissionalId, c.Inicio, c.Fim)).ToList();
        if (cursoId is not null)
            return new FatosProducao([], [], [], atendimentos.Concat(atividades).ToList(), pendentes, []);

        // Só as realizações registradas, pelo horário efetivo.
        var realizados = (await db.Compromissos.AsNoTracking()
                .Where(c => ids.Contains(c.ProfissionalId) && c.Estado == EstadoCompromisso.Concluido
                    && c.RealizacaoInicio != null && c.RealizacaoInicio >= de && c.RealizacaoInicio < ate)
                .Select(c => new { c.ProfissionalId, c.Tipo, Inicio = c.RealizacaoInicio!.Value, Fim = c.RealizacaoFim!.Value })
                .ToListAsync(ct))
            .Select(c => new OcorrenciaProfissional(c.ProfissionalId, c.Tipo.CategoriaDe()!.Value, c.Inicio, c.Fim, false));
        // Remarcado foi substituído pelo novo compromisso; contá-lo descontaria a capacidade duas vezes.
        var bloqueios = (await db.Compromissos.AsNoTracking()
                .Where(c => ids.Contains(c.ProfissionalId) && c.Tipo == TipoCompromisso.Bloqueio
                    && c.Estado != EstadoCompromisso.Cancelado && c.Estado != EstadoCompromisso.Remarcado
                    && c.Intervalo.Inicio < ate && c.Intervalo.Fim > de)
                .Select(c => new { c.ProfissionalId, c.Intervalo.Inicio, c.Intervalo.Fim })
                .ToListAsync(ct))
            .Select(c => new IntervaloProfissional(c.ProfissionalId, c.Inicio, c.Fim)).ToList();
        var escalas = await db.EscalasProfissional.AsNoTracking()
            .Where(e => ids.Contains(e.ProfissionalId) && e.VigenciaInicio <= fim && (e.VigenciaFim == null || e.VigenciaFim >= inicio))
            .ToListAsync(ct);
        var feriados = await db.Feriados.AsNoTracking().Where(f => f.Ativo && f.Data >= inicio && f.Data <= fim).Select(f => f.Data)
            .ToListAsync(ct);
        var ajustes = await db.AjustesProducao.AsNoTracking()
            .Where(a => ids.Contains(a.ProfissionalId) && a.Data >= inicio && a.Data <= fim)
            .ToListAsync(ct);
        return new FatosProducao(escalas, feriados, bloqueios, atendimentos.Concat(atividades).Concat(realizados).ToList(), pendentes, ajustes);
    }

    public async Task<IReadOnlyList<AjusteProducaoDto>> ListarAjustesAsync(Guid? profissionalId, DateOnly inicio, DateOnly fim,
        CancellationToken ct) =>
        (await db.AjustesProducao.AsNoTracking()
            .Where(a => a.Data >= inicio && a.Data <= fim && (profissionalId == null || a.ProfissionalId == profissionalId))
            .OrderByDescending(a => a.Data).ThenByDescending(a => a.Em).ThenBy(a => a.Id)
            .Select(a => new
            {
                a.Id, a.ProfissionalId, a.Data, a.Categoria, a.Ocorrencias, a.Minutos, a.Justificativa, a.Em,
                Profissional = db.Users.Where(u => u.Id == a.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                Autor = db.Users.Where(u => u.Id == a.AutorId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
            })
            .ToListAsync(ct))
        .Select(a => new AjusteProducaoDto(a.Id, a.ProfissionalId, a.Profissional, a.Data, a.Categoria, a.Categoria.Rotulo(), a.Ocorrencias,
            a.Minutos, a.Justificativa, a.Autor, a.Em))
        .ToList();
}
