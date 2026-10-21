using Nap.Application.Features.Agenda;

namespace Nap.Application.Tests.Fakes;

public sealed class AgendaQueriesFalso : IAgendaQueries
{
    public Task<AgendaSemanaDto> ListarSemanaAsync(DateOnly segunda, Guid? profissionalId, CancellationToken ct) =>
        Task.FromResult(new AgendaSemanaDto(segunda, segunda.AddDays(4), [], [], []));
}
