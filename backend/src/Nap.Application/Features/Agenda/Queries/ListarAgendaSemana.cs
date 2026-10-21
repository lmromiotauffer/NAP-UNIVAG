using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Agenda.Queries;

public record ListarAgendaSemanaQuery(DateOnly Inicio, Guid? ProfissionalId = null);

public class ListarAgendaSemanaValidator : AbstractValidator<ListarAgendaSemanaQuery>
{
    public ListarAgendaSemanaValidator() =>
        RuleFor(x => x.Inicio).Must(d => d.DayOfWeek == DayOfWeek.Monday).WithMessage("A semana deve iniciar na segunda-feira.");
}

public sealed class ListarAgendaSemanaHandler(IValidator<ListarAgendaSemanaQuery> validador, IAgendaQueries consultas, IUsuarioAtual usuario)
{
    public async Task<AgendaSemanaDto> Handle(ListarAgendaSemanaQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();
        return await consultas.ListarSemanaAsync(query.Inicio, query.ProfissionalId, ct);
    }
}
