using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Agenda.Queries;

/// <summary><c>Inicio</c>/<c>Fim</c> (datas no fuso institucional, inclusivas): autorizações cujo intervalo toca o período.</summary>
public record ListarAutorizacoesForaEscalaQuery(Guid? ProfissionalId, DateOnly? Inicio, DateOnly? Fim, int Pagina = 1, int Tamanho = 20);

public class ListarAutorizacoesForaEscalaValidator : AbstractValidator<ListarAutorizacoesForaEscalaQuery>
{
    public ListarAutorizacoesForaEscalaValidator()
    {
        RuleFor(x => x.Pagina).InclusiveBetween(1, 100_000);
        RuleFor(x => x.Tamanho).InclusiveBetween(1, 100);
        RuleFor(x => x.Fim!.Value).GreaterThanOrEqualTo(x => x.Inicio!.Value).WithMessage("O fim deve ser igual ou posterior ao início.")
            .OverridePropertyName(nameof(ListarAutorizacoesForaEscalaQuery.Fim))
            .When(x => x.Inicio is not null && x.Fim is not null);
    }
}

public sealed class ListarAutorizacoesForaEscalaHandler(IValidator<ListarAutorizacoesForaEscalaQuery> validador, IEscalaRepository escalas,
    IUsuarioAtual usuario, IRelogio relogio)
{
    public async Task<PagedResult<AutorizacaoForaEscalaDto>> Handle(ListarAutorizacoesForaEscalaQuery query, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(query, ct);
        DateTimeOffset? de = query.Inicio is { } i ? relogio.Fuso.InicioDoDia(i) : null;
        DateTimeOffset? ate = query.Fim is { } f ? relogio.Fuso.InicioDoDia(f.AddDays(1)) : null;
        return await escalas.ListarAutorizacoesAsync(query.ProfissionalId, de, ate, query.Pagina, query.Tamanho, ct);
    }
}
