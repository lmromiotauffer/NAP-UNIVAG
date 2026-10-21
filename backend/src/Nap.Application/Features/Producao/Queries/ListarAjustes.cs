using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Producao.Queries;

public record ListarAjustesQuery(Guid? ProfissionalId, DateOnly Inicio, DateOnly Fim);

public class ListarAjustesValidator : AbstractValidator<ListarAjustesQuery>
{
    public ListarAjustesValidator()
    {
        RuleFor(x => x.Fim).GreaterThanOrEqualTo(x => x.Inicio).WithMessage("O fim deve ser igual ou posterior ao início.")
            .Must((x, fim) => fim.DayNumber - x.Inicio.DayNumber <= 366).WithMessage("O período não pode passar de um ano.");
    }
}

public sealed class ListarAjustesHandler(IValidator<ListarAjustesQuery> validador, IProducaoQueries consultas, IUsuarioAtual usuario)
{
    public async Task<IReadOnlyList<AjusteProducaoDto>> Handle(ListarAjustesQuery consulta, CancellationToken ct = default)
    {
        await validador.ValidarAsync(consulta, ct);
        if (!usuario.EhCoordenacao()) throw new ForbiddenException();
        return await consultas.ListarAjustesAsync(consulta.ProfissionalId, consulta.Inicio, consulta.Fim, ct);
    }
}
