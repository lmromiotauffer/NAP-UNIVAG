using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Auditoria.Queries;

public record ListarAuditoriaQuery(DateOnly? Inicio, DateOnly? Fim, Guid? UsuarioId = null, string? Acao = null,
    string? Recurso = null, Guid? RecursoId = null, string? Resultado = null, int Pagina = 1, int Tamanho = 20);

public class ListarAuditoriaValidator : AbstractValidator<ListarAuditoriaQuery>
{
    public ListarAuditoriaValidator()
    {
        RuleFor(x => x.Pagina).InclusiveBetween(1, 100_000);
        RuleFor(x => x.Tamanho).InclusiveBetween(1, 100);
        RuleFor(x => x.Acao).MaximumLength(100);
        RuleFor(x => x.Recurso).MaximumLength(100);
        RuleFor(x => x.Resultado).MaximumLength(40);
        RuleFor(x => x.Inicio).NotNull().WithMessage("Informe o início.");
        RuleFor(x => x.Fim).NotNull().WithMessage("Informe o fim.");
        // Até 366 dias contando o dia final.
        RuleFor(x => x.Fim!.Value).GreaterThanOrEqualTo(x => x.Inicio!.Value).WithMessage("O fim deve ser igual ou posterior ao início.")
            .Must((x, fim) => fim.DayNumber - x.Inicio!.Value.DayNumber <= 365).WithMessage("O período não pode passar de 366 dias.")
            .OverridePropertyName(nameof(ListarAuditoriaQuery.Fim))
            .When(x => x.Inicio is not null && x.Fim is not null);
    }
}

/// <summary>Trilha de auditoria (somente leitura) para a Coordenação; cada consulta também é auditada.</summary>
public sealed class ListarAuditoriaHandler(IValidator<ListarAuditoriaQuery> validador, IAuditoriaQueries consultas, IUsuarioAtual usuario,
    IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task<PagedResult<EventoAuditoriaDto>> Handle(ListarAuditoriaQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        if (!usuario.EhCoordenacao()) throw new ForbiddenException();
        var pagina = await consultas.ListarAsync(new FiltroAuditoria(query.Inicio!.Value, query.Fim!.Value, query.UsuarioId, query.Acao, query.Recurso,
            query.RecursoId, query.Resultado, query.Pagina, query.Tamanho), ct);
        auditoria.Registrar("auditoria.consulta", "Auditoria", null);
        await uow.SalvarAsync(ct);
        return pagina;
    }
}
