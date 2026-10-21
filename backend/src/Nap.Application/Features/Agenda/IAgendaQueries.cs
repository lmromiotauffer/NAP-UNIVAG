namespace Nap.Application.Features.Agenda;

public interface IAgendaQueries
{
    Task<AgendaSemanaDto> ListarSemanaAsync(DateOnly segunda, Guid? profissionalId, CancellationToken ct);
}
