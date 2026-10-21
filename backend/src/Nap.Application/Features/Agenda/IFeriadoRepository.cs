using Nap.Domain.Agenda;

namespace Nap.Application.Features.Agenda;

/// <summary>O que já ocupa a data do feriado: compromisso Agendado que ocupa horário ou atendimento Realizado/Ausente.</summary>
public record ConflitoFeriadoDto(Guid Id, string Tipo, string Titulo, Guid ProfissionalId, string Profissional,
    DateTimeOffset Inicio, DateTimeOffset Fim);

public interface IFeriadoRepository
{
    Task<Feriado?> ObterAsync(Guid id, CancellationToken ct);
    Task<Feriado?> ObterAtivoAsync(DateOnly data, CancellationToken ct);
    /// <summary>Feriados ativos de <paramref name="de"/> a <paramref name="ate"/> (inclusive), por data.</summary>
    Task<IReadOnlyList<Feriado>> ListarAtivosAsync(DateOnly de, DateOnly ate, CancellationToken ct);
    /// <summary>Ocupações na data civil (fuso institucional), por início.</summary>
    Task<IReadOnlyList<ConflitoFeriadoDto>> ListarConflitosAsync(DateOnly data, CancellationToken ct);
    void Adicionar(Feriado f);
}
