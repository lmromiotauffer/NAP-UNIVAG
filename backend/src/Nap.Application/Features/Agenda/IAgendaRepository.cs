using Nap.Application.Comum;
using Nap.Domain.Agenda;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Features.Agenda;

/// <summary>Item que ocupa o horário do bloqueio (ou de um participante do grupo). <c>Aluno</c> só quando a agenda já o exibe a quem consulta (equipe NAP).</summary>
public record ConflitoAgendaDto(Guid Id, string Tipo, DateTimeOffset Inicio, DateTimeOffset Fim, string? Aluno, Guid ProfissionalId,
    string Profissional);

public interface IAgendaRepository
{
    Task<Compromisso?> ObterAsync(Guid id, CancellationToken ct);

    /// <summary>Serializa gravações concorrentes nas agendas envolvidas. Só é válido dentro de EmTransacaoAsync.</summary>
    Task BloquearAgendasAsync(Guid profissionalId, Guid? alunoId, CancellationToken ct);

    /// <summary>Mesma trava, para as agendas de vários profissionais (participantes de um grupo), em ordem lexicográfica das chaves.</summary>
    Task BloquearAgendasAsync(IReadOnlyCollection<Guid> profissionaisIds, CancellationToken ct);

    Task<bool> ExisteConflitoAsync(Guid profissionalId, Guid? alunoId, IntervaloHorario intervalo, Guid? ignorarId, CancellationToken ct);

    /// <summary>A mesma regra de <see cref="ExisteConflitoAsync"/> (sem aluno) para vários profissionais numa consulta por fonte:
    /// devolve os que têm conflito. <paramref name="ignorarIds"/> são as instâncias sendo remarcadas (cada uma de um só participante).</summary>
    Task<IReadOnlyList<Guid>> ProfissionaisComConflitoAsync(IReadOnlyCollection<Guid> profissionaisIds, IntervaloHorario intervalo,
        IReadOnlyCollection<Guid> ignorarIds, CancellationToken ct);

    /// <summary>Itens do profissional que ocupam o intervalo, pela mesma regra de <see cref="ExisteConflitoAsync"/>. Dentro de EmTransacaoAsync, após BloquearAgendasAsync.</summary>
    Task<IReadOnlyList<ConflitoAgendaDto>> ListarConflitosAsync(Guid profissionalId, IntervaloHorario intervalo, Guid? ignorarId, CancellationToken ct);

    /// <summary>Todas as instâncias do grupo, em qualquer estado (rastreadas).</summary>
    Task<IReadOnlyList<Compromisso>> ListarDoGrupoAsync(Guid grupoId, CancellationToken ct);

    void Adicionar(Compromisso c);
}

public static class AgendaRepositoryExtensoes
{
    /// <summary>Bloqueia as agendas e, se o horário é ocupado, falha em caso de conflito. Chamar dentro de EmTransacaoAsync.</summary>
    public static async Task GarantirSemConflitoAsync(this IAgendaRepository agenda, Guid profissionalId, Guid? alunoId,
        IntervaloHorario intervalo, Guid? ignorarId, bool ocupaHorario, CancellationToken ct, bool listarConflitos = false)
    {
        await agenda.BloquearAgendasAsync(profissionalId, alunoId, ct);
        if (ocupaHorario && await agenda.ExisteConflitoAsync(profissionalId, alunoId, intervalo, ignorarId, ct))
        {
            // Bloqueio: a lista sai sob as mesmas travas da checagem; a exceção desfaz a transação e nada é criado.
            if (listarConflitos && await agenda.ListarConflitosAsync(profissionalId, intervalo, ignorarId, ct) is { Count: > 0 } conflitos)
                throw new ConflitoAgendaException(conflitos);
            throw new ConflictException(ConflitoAgendaException.MensagemPadrao);
        }
    }

    /// <summary>
    /// Versão do grupo: trava as agendas de todos os participantes de uma vez e, se o horário é ocupado, falha listando os conflitos de
    /// cada um (nenhuma instância é gravada). <c>IgnorarId</c> é a instância do participante sendo remarcada. Dentro de EmTransacaoAsync,
    /// depois das travas de fechamento de todos.
    /// </summary>
    public static async Task GarantirGrupoSemConflitoAsync(this IAgendaRepository agenda,
        IReadOnlyCollection<(Guid ProfissionalId, Guid? IgnorarId)> participantes, IntervaloHorario intervalo, bool ocupaHorario, CancellationToken ct)
    {
        await agenda.BloquearAgendasAsync([.. participantes.Select(p => p.ProfissionalId)], ct);
        if (!ocupaHorario) return;
        // Uma checagem para o grupo todo; a lista detalhada só para quem conflita (caso raro).
        var comConflito = await agenda.ProfissionaisComConflitoAsync([.. participantes.Select(p => p.ProfissionalId)], intervalo,
            [.. participantes.Where(p => p.IgnorarId is not null).Select(p => p.IgnorarId!.Value)], ct);
        if (comConflito.Count == 0) return;
        var conflitos = new List<ConflitoAgendaDto>();
        foreach (var (profissionalId, ignorarId) in participantes.Where(p => comConflito.Contains(p.ProfissionalId)))
            conflitos.AddRange(await agenda.ListarConflitosAsync(profissionalId, intervalo, ignorarId, ct));
        if (conflitos.Count > 0) throw new ConflitoAgendaException(conflitos);
        throw new ConflictException(ConflitoAgendaException.MensagemPadrao);
    }
}
