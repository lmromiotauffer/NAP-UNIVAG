using Nap.Application.Features.Agenda;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Tests.Fakes;

/// <summary>Mesma semântica do AgendaRepository: ocupam horário os compromissos Agendados (exceto Lembrete) e os
/// atendimentos Realizados/Ausentes (da agenda ou avulsos).</summary>
public sealed class AgendaRepositoryEmMemoria(List<string>? ordem = null, List<Atendimento>? atendimentos = null) : IAgendaRepository
{
    public List<Compromisso> Compromissos { get; } = [];

    /// <summary>Log compartilhado com o fake de fechamento: chaves <c>agenda:prof:{id:N}</c> / <c>fechamento:prof:{id:N}</c> na ordem das travas.</summary>
    public List<string>? Travas { get; init; }

    public Task<Compromisso?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Compromissos.FirstOrDefault(c => c.Id == id));

    public Task BloquearAgendasAsync(Guid profissionalId, Guid? alunoId, CancellationToken ct)
    {
        ordem?.Add("bloquear");
        Travas?.Add($"agenda:prof:{profissionalId:N}");
        return Task.CompletedTask;
    }

    public Task BloquearAgendasAsync(IReadOnlyCollection<Guid> profissionaisIds, CancellationToken ct)
    {
        ordem?.Add("bloquear");
        foreach (var id in profissionaisIds.Distinct()) Travas?.Add($"agenda:prof:{id:N}");
        return Task.CompletedTask;
    }

    public Task<bool> ExisteConflitoAsync(Guid profissionalId, Guid? alunoId, IntervaloHorario intervalo, Guid? ignorarId, CancellationToken ct)
    {
        ordem?.Add("conflito");
        return ConflitaAsync(profissionalId, alunoId, intervalo, id => id == ignorarId);
    }

    private Task<bool> ConflitaAsync(Guid profissionalId, Guid? alunoId, IntervaloHorario intervalo, Func<Guid, bool> ignorado)
    {
        var compromisso = Compromissos.Any(c => !ignorado(c.Id) && c.Estado == EstadoCompromisso.Agendado
            && c.Tipo.OcupaHorario() && c.Intervalo.Inicio < intervalo.Fim && intervalo.Inicio < c.Intervalo.Fim
            && (c.ProfissionalId == profissionalId || (alunoId is not null && c.AlunoId == alunoId)));
        var atendimento = (atendimentos ?? []).Any(a => !ignorado(a.Id)
            && a.Estado is EstadoAtendimento.Realizado or EstadoAtendimento.Ausente && a.Intervalo.Inicio < intervalo.Fim && intervalo.Inicio < a.Intervalo.Fim
            && (a.ProfissionalId == profissionalId || (alunoId is not null && a.AlunoId == alunoId)));
        return Task.FromResult(compromisso || atendimento);
    }

    public async Task<IReadOnlyList<Guid>> ProfissionaisComConflitoAsync(IReadOnlyCollection<Guid> profissionaisIds, IntervaloHorario intervalo,
        IReadOnlyCollection<Guid> ignorarIds, CancellationToken ct)
    {
        // Uma entrada em ordem por chamada, como o repositório real faz uma checagem para o grupo.
        ordem?.Add("conflito");
        var com = new List<Guid>();
        foreach (var id in profissionaisIds)
            if (await ConflitaAsync(id, null, intervalo, ignorarIds.Contains)) com.Add(id);
        return com;
    }

    public Task<IReadOnlyList<ConflitoAgendaDto>> ListarConflitosAsync(Guid profissionalId, IntervaloHorario intervalo, Guid? ignorarId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ConflitoAgendaDto>>([.. Compromissos
            .Where(c => c.Id != ignorarId && c.Estado == EstadoCompromisso.Agendado && c.Tipo.OcupaHorario()
                && c.Intervalo.Inicio < intervalo.Fim && intervalo.Inicio < c.Intervalo.Fim && c.ProfissionalId == profissionalId)
            .Select(c => new ConflitoAgendaDto(c.Id, "Compromisso", c.Intervalo.Inicio, c.Intervalo.Fim, null, c.ProfissionalId, ""))]);

    public Task<IReadOnlyList<Compromisso>> ListarDoGrupoAsync(Guid grupoId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Compromisso>>([.. Compromissos.Where(c => c.GrupoId == grupoId)]);

    public void Adicionar(Compromisso c) => Compromissos.Add(c);
}
