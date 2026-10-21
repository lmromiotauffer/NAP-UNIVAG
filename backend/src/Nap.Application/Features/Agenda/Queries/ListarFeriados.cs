using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Agenda.Queries;

public record ListarFeriadosQuery(int Ano);

public record FeriadoDto(Guid Id, DateOnly Data, string Descricao);

public class ListarFeriadosValidator : AbstractValidator<ListarFeriadosQuery>
{
    public ListarFeriadosValidator() => RuleFor(x => x.Ano).InclusiveBetween(2000, 2100);
}

/// <summary>Aberta a todos os autenticados: a agenda mostra os feriados.</summary>
public sealed class ListarFeriadosHandler(IValidator<ListarFeriadosQuery> validador, IFeriadoRepository feriados)
{
    public async Task<IReadOnlyList<FeriadoDto>> Handle(ListarFeriadosQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        var lista = await feriados.ListarAtivosAsync(new DateOnly(query.Ano, 1, 1), new DateOnly(query.Ano, 12, 31), ct);
        return lista.Select(f => new FeriadoDto(f.Id, f.Data, f.Descricao)).ToList();
    }
}
