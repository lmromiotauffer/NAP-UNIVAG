using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Atividades;
using Nap.Domain.Comum.ValueObjects;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class AgendaRepository(NapDbContext db) : IAgendaRepository
{
    public Task<Compromisso?> ObterAsync(Guid id, CancellationToken ct) =>
        db.Compromissos.SingleOrDefaultAsync(c => c.Id == id, ct);

    /// <summary>Trava de aplicação (sp_getapplock) por agenda, presa à transação; chaves em ordem ordinal evitam deadlock.</summary>
    public Task BloquearAgendasAsync(Guid profissionalId, Guid? alunoId, CancellationToken ct)
    {
        var chaves = new List<string> { $"agenda:prof:{profissionalId:N}" };
        if (alunoId is { } aluno) chaves.Add($"agenda:aluno:{aluno:N}");
        return BloquearAsync(chaves, ct);
    }

    public Task BloquearAgendasAsync(IReadOnlyCollection<Guid> profissionaisIds, CancellationToken ct) =>
        BloquearAsync([.. profissionaisIds.Distinct().Select(id => $"agenda:prof:{id:N}")], ct);

    private async Task BloquearAsync(List<string> chaves, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("O bloqueio de agenda só é válido dentro de EmTransacaoAsync.");
        chaves.Sort(StringComparer.Ordinal);

        try
        {
            foreach (var chave in chaves)
                await db.Database.ExecuteSqlRawAsync(
                    "DECLARE @r int; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @r < 0 THROW 51000, 'Agenda ocupada; tente novamente', 1;",
                    [chave], ct);
        }
        catch (SqlException e) when (e.Number == 51000)
        {
            throw new ConflictException("Agenda ocupada; tente novamente");
        }
    }

    /// <summary>
    /// Sobreposição (<c>inicio &lt; fimNovo AND fim &gt; inicioNovo</c>; horários consecutivos são permitidos) do profissional
    /// OU do aluno contra: (a) compromissos Agendados que ocupam horário (não Lembrete); (b) TODOS os atendimentos Realizados
    /// ou Ausentes, vindos da agenda ou avulsos; (c) TODAS as atividades nos cursos, só do profissional (sem aluno); (d) compromissos Concluídos com realização registrada
    /// (reuniões, PEI, tarefas), pelo horário efetivo — o agendado original fica livre. O atendimento é a fonte do tempo ocupado: a correção dele (horário ou estado)
    /// não mexe no compromisso concluído, que passa a ser só um ponteiro para o registro. Se o Concluído contasse, continuaria
    /// bloqueando o horário antigo e liberaria o novo. O mesmo vale para a atividade. <paramref name="ignorarId"/> exclui o
    /// compromisso (agendado ou concluído com realização), o atendimento e a atividade com esse Id.
    /// </summary>
    public async Task<bool> ExisteConflitoAsync(Guid profissionalId, Guid? alunoId, IntervaloHorario intervalo, Guid? ignorarId, CancellationToken ct) =>
        await Agendados(profissionalId, alunoId, intervalo, ignorarId).AnyAsync(ct)
        || await Realizados(profissionalId, alunoId, intervalo, ignorarId).AnyAsync(ct)
        || await Atendimentos(profissionalId, alunoId, intervalo, ignorarId).AnyAsync(ct)
        || await Atividades(profissionalId, intervalo, ignorarId).AnyAsync(ct);

    public async Task<IReadOnlyList<Guid>> ProfissionaisComConflitoAsync(IReadOnlyCollection<Guid> profissionaisIds, IntervaloHorario intervalo,
        IReadOnlyCollection<Guid> ignorarIds, CancellationToken ct)
    {
        var ids = profissionaisIds.ToList();
        var ignorar = ignorarIds.ToList();
        var (inicio, fim) = (intervalo.Inicio, intervalo.Fim);
        var agendados = await Agendados(db, inicio, fim, ignorar, ids, null).Select(c => c.ProfissionalId).Distinct().ToListAsync(ct);
        var realizados = await Realizados(db, inicio, fim, ignorar, ids, null).Select(c => c.ProfissionalId).Distinct().ToListAsync(ct);
        var atendimentos = await Atendimentos(db, inicio, fim, ignorar, ids, null).Select(a => a.ProfissionalId).Distinct().ToListAsync(ct);
        var atividades = await Atividades(db, inicio, fim, ignorar, ids).Select(a => a.ProfissionalId).Distinct().ToListAsync(ct);
        return [.. agendados.Union(realizados).Union(atendimentos).Union(atividades)];
    }

    /// <summary>Os mesmos ocupantes de <see cref="ExisteConflitoAsync"/>, só do profissional. O nome do aluno é o que a agenda semanal já exibe à equipe NAP.</summary>
    public async Task<IReadOnlyList<ConflitoAgendaDto>> ListarConflitosAsync(Guid profissionalId, IntervaloHorario intervalo, Guid? ignorarId, CancellationToken ct)
    {
        var nome = await db.Users.Where(u => u.Id == profissionalId).Select(u => u.NomeCompleto).FirstOrDefaultAsync(ct) ?? "";

        var agendados = await Agendados(profissionalId, null, intervalo, ignorarId).AsNoTracking()
            .Select(c => new ConflitoAgendaDto(c.Id, "Compromisso", c.Intervalo.Inicio, c.Intervalo.Fim,
                db.Alunos.Where(a => a.Id == c.AlunoId).Select(a => a.NomeCompleto).FirstOrDefault(), profissionalId, nome))
            .ToListAsync(ct);

        var realizados = await Realizados(profissionalId, null, intervalo, ignorarId).AsNoTracking()
            .Select(c => new ConflitoAgendaDto(c.Id, "Compromisso", c.RealizacaoInicio!.Value, c.RealizacaoFim!.Value,
                db.Alunos.Where(a => a.Id == c.AlunoId).Select(a => a.NomeCompleto).FirstOrDefault(), profissionalId, nome))
            .ToListAsync(ct);

        var atendimentos = await Atendimentos(profissionalId, null, intervalo, ignorarId).AsNoTracking()
            .Select(a => new ConflitoAgendaDto(a.Id, "Atendimento", a.Intervalo.Inicio, a.Intervalo.Fim,
                db.Alunos.Where(l => l.Id == a.AlunoId).Select(l => l.NomeCompleto).FirstOrDefault(), profissionalId, nome))
            .ToListAsync(ct);

        var atividades = await Atividades(profissionalId, intervalo, ignorarId).AsNoTracking()
            .Select(a => new ConflitoAgendaDto(a.Id, "Atividade", a.Intervalo.Inicio, a.Intervalo.Fim, null, profissionalId, nome))
            .ToListAsync(ct);

        return agendados.Concat(realizados).Concat(atendimentos).Concat(atividades).OrderBy(c => c.Inicio).ThenBy(c => c.Id).ToList();
    }

    // Uma única definição de cada fonte de ocupação, usada pela checagem (Any), pela lista do 409 (Select) e pela prévia de
    // conflitos do feriado (FeriadoRepository): mudar a regra em uma sem a outra deixaria o 409 com uma lista que não
    // corresponde ao que bloqueou. Com alunoId nulo, só a agenda dos profissionais; profissionais nulo = todos.
    // O início fica limitado por baixo pela duração máxima (IntervaloHorario): o índice (ProfissionalId, Inicio) não varre o histórico.
    private IQueryable<Compromisso> Agendados(Guid profissionalId, Guid? alunoId, IntervaloHorario intervalo, Guid? ignorarId) =>
        Agendados(db, intervalo.Inicio, intervalo.Fim, Ignorar(ignorarId), [profissionalId], alunoId);

    private IQueryable<Compromisso> Realizados(Guid profissionalId, Guid? alunoId, IntervaloHorario intervalo, Guid? ignorarId) =>
        Realizados(db, intervalo.Inicio, intervalo.Fim, Ignorar(ignorarId), [profissionalId], alunoId);

    private IQueryable<Atendimento> Atendimentos(Guid profissionalId, Guid? alunoId, IntervaloHorario intervalo, Guid? ignorarId) =>
        Atendimentos(db, intervalo.Inicio, intervalo.Fim, Ignorar(ignorarId), [profissionalId], alunoId);

    private IQueryable<AtividadeCurso> Atividades(Guid profissionalId, IntervaloHorario intervalo, Guid? ignorarId) =>
        Atividades(db, intervalo.Inicio, intervalo.Fim, Ignorar(ignorarId), [profissionalId]);

    private static List<Guid> Ignorar(Guid? ignorarId) => ignorarId is { } id ? [id] : [];

    private static DateTimeOffset Minimo(DateTimeOffset inicio) => inicio.AddHours(-PainelQueries.DuracaoMaximaHoras);

    internal static IQueryable<Compromisso> Agendados(NapDbContext contexto, DateTimeOffset inicio, DateTimeOffset fim, List<Guid> ignorar,
        List<Guid>? profissionais, Guid? alunoId)
    {
        var minimo = Minimo(inicio);
        var q = contexto.Compromissos.Where(c =>
            !ignorar.Contains(c.Id)
            && c.Estado == EstadoCompromisso.Agendado
            && c.Tipo != TipoCompromisso.Lembrete
            && c.Intervalo.Inicio >= minimo && c.Intervalo.Inicio < fim && c.Intervalo.Fim > inicio);
        return profissionais is null ? q : q.Where(c => profissionais.Contains(c.ProfissionalId) || (alunoId != null && c.AlunoId == alunoId));
    }

    internal static IQueryable<Compromisso> Realizados(NapDbContext contexto, DateTimeOffset inicio, DateTimeOffset fim, List<Guid> ignorar,
        List<Guid>? profissionais, Guid? alunoId)
    {
        var minimo = Minimo(inicio);
        var q = contexto.Compromissos.Where(c =>
            !ignorar.Contains(c.Id)
            && c.Estado == EstadoCompromisso.Concluido && c.RealizacaoInicio != null
            && c.RealizacaoInicio >= minimo && c.RealizacaoInicio < fim && c.RealizacaoFim > inicio);
        return profissionais is null ? q : q.Where(c => profissionais.Contains(c.ProfissionalId) || (alunoId != null && c.AlunoId == alunoId));
    }

    internal static IQueryable<Atendimento> Atendimentos(NapDbContext contexto, DateTimeOffset inicio, DateTimeOffset fim, List<Guid> ignorar,
        List<Guid>? profissionais, Guid? alunoId)
    {
        var minimo = Minimo(inicio);
        var q = contexto.Atendimentos.Where(a =>
            !ignorar.Contains(a.Id)
            && (a.Estado == EstadoAtendimento.Realizado || a.Estado == EstadoAtendimento.Ausente)
            && a.Intervalo.Inicio >= minimo && a.Intervalo.Inicio < fim && a.Intervalo.Fim > inicio);
        return profissionais is null ? q : q.Where(a => profissionais.Contains(a.ProfissionalId) || (alunoId != null && a.AlunoId == alunoId));
    }

    internal static IQueryable<AtividadeCurso> Atividades(NapDbContext contexto, DateTimeOffset inicio, DateTimeOffset fim, List<Guid> ignorar,
        List<Guid>? profissionais)
    {
        var minimo = Minimo(inicio);
        var q = contexto.AtividadesCurso.Where(a =>
            !ignorar.Contains(a.Id)
            && a.Intervalo.Inicio >= minimo && a.Intervalo.Inicio < fim && a.Intervalo.Fim > inicio);
        return profissionais is null ? q : q.Where(a => profissionais.Contains(a.ProfissionalId));
    }

    public async Task<IReadOnlyList<Compromisso>> ListarDoGrupoAsync(Guid grupoId, CancellationToken ct) =>
        await db.Compromissos.Where(c => c.GrupoId == grupoId).ToListAsync(ct);

    public void Adicionar(Compromisso c) => db.Compromissos.Add(c);
}
