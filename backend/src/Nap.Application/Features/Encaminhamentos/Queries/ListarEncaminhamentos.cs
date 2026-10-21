using FluentValidation;
using Nap.Application.Comum;
using Nap.Domain.Encaminhamentos;

namespace Nap.Application.Features.Encaminhamentos.Queries;

public record ListarEncaminhamentosQuery(Guid? AlunoId = null, Guid? CursoId = null, Especialidade? Especialidade = null,
    DestinoEncaminhamento? Destino = null, EstadoEncaminhamento? Estado = null, DateOnly? De = null, DateOnly? Ate = null,
    bool? SemAcolhimentoNoPrazo = null, int Pagina = 1, int Tamanho = 20);

public class ListarEncaminhamentosValidator : AbstractValidator<ListarEncaminhamentosQuery>
{
    public ListarEncaminhamentosValidator()
    {
        RuleFor(x => x.Pagina).InclusiveBetween(1, 100_000);
        RuleFor(x => x.Tamanho).InclusiveBetween(1, 100);
    }
}

public sealed class ListarEncaminhamentosHandler(IValidator<ListarEncaminhamentosQuery> validador, IEncaminhamentoQueries consultas,
    IUsuarioAtual usuario, IRelogio relogio, IConfiguracaoAlertas alertas)
{
    public async Task<PagedResult<EncaminhamentoListaDto>> Handle(ListarEncaminhamentosQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        var escopo = EscopoEncaminhamentos.Para(usuario);
        var filtro = new FiltroEncaminhamentos(query.AlunoId, query.CursoId, query.Especialidade, query.Destino, query.Estado,
            query.De, query.Ate, query.SemAcolhimentoNoPrazo, query.Pagina, query.Tamanho);
        return await consultas.ListarAsync(filtro, escopo, relogio.Hoje, (await alertas.ObterDtoAsync(ct)).DiasSemAcolhimento, ct);
    }
}
