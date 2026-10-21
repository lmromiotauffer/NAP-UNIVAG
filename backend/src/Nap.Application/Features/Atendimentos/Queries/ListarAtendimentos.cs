using FluentValidation;
using Nap.Application.Comum;
using Nap.Domain.Atendimentos;

namespace Nap.Application.Features.Atendimentos.Queries;

public record ListarAtendimentosQuery(Guid? AlunoId = null, Guid? ProfissionalId = null, EstadoAtendimento? Estado = null,
    TipoAtendimento? Tipo = null, DateOnly? De = null, DateOnly? Ate = null, int Pagina = 1, int Tamanho = 20);

public class ListarAtendimentosValidator : AbstractValidator<ListarAtendimentosQuery>
{
    public ListarAtendimentosValidator()
    {
        RuleFor(x => x.Pagina).InclusiveBetween(1, 100_000);
        RuleFor(x => x.Tamanho).InclusiveBetween(1, 100);
    }
}

public sealed class ListarAtendimentosHandler(IValidator<ListarAtendimentosQuery> validador, IAtendimentoQueries consultas, IUsuarioAtual usuario)
{
    public async Task<PagedResult<AtendimentoListaDto>> Handle(ListarAtendimentosQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();
        return await consultas.ListarAsync(new FiltroAtendimentos(query.AlunoId, query.ProfissionalId, query.Estado, query.Tipo,
            query.De, query.Ate, query.Pagina, query.Tamanho), ct);
    }
}
