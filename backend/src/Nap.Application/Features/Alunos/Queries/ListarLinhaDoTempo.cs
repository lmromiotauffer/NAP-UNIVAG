using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Alunos.Queries;

public record ListarLinhaDoTempoQuery(Guid AlunoId, int Pagina = 1, int Tamanho = 20);

public class ListarLinhaDoTempoValidator : AbstractValidator<ListarLinhaDoTempoQuery>
{
    public const int LimiteEventos = 2_000;

    public ListarLinhaDoTempoValidator()
    {
        RuleFor(x => x.AlunoId).NotEmpty();
        // Cada fonte carrega até pagina × tamanho eventos para montar a página em memória: o teto limita essa leitura.
        RuleFor(x => x.Pagina).InclusiveBetween(1, 100_000)
            .Must((x, pagina) => (long)pagina * x.Tamanho <= LimiteEventos)
            .WithMessage($"A linha do tempo lista até {LimiteEventos} eventos.");
        RuleFor(x => x.Tamanho).InclusiveBetween(1, 100);
    }
}

public sealed class ListarLinhaDoTempoHandler(IValidator<ListarLinhaDoTempoQuery> validador, IAlunoQueries consultas,
    IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task<PagedResult<EventoLinhaDoTempoDto>> Handle(ListarLinhaDoTempoQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        var escopo = EscopoAlunos.Para(usuario);
        // Mesma regra de escopo da ficha: onde ela daria 404, a linha do tempo também.
        var pagina = await consultas.LinhaDoTempoAsync(query.AlunoId, escopo, query.Pagina, query.Tamanho, ct)
            ?? throw new NotFoundException("Aluno");
        auditoria.Registrar("aluno.linha-do-tempo.consulta", "Aluno", query.AlunoId);
        await uow.SalvarAsync(ct);
        return pagina;
    }
}
