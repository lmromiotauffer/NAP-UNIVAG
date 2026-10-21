using FluentValidation;
using Nap.Application.Comum;
using Nap.Domain.Alunos;
using Nap.Domain.Encaminhamentos;

namespace Nap.Application.Features.Alunos.Queries;

public record ListarAlunosQuery(string? Busca = null, Guid? CursoId = null, Guid? ProfissionalId = null,
    SituacaoAluno? Situacao = null, DateOnly? InicioDe = null, DateOnly? InicioAte = null,
    int Pagina = 1, int Tamanho = 20, Especialidade? EspecialidadeEncaminhamento = null);

public class ListarAlunosValidator : AbstractValidator<ListarAlunosQuery>
{
    public ListarAlunosValidator()
    {
        RuleFor(x => x.Pagina).InclusiveBetween(1, 100_000);
        RuleFor(x => x.Tamanho).InclusiveBetween(1, 100);
        RuleFor(x => x.Busca).MaximumLength(200);
    }
}

public sealed class ListarAlunosHandler(IValidator<ListarAlunosQuery> validador, IAlunoQueries consultas, IUsuarioAtual usuario)
{
    public async Task<PagedResult<AlunoListaDto>> Handle(ListarAlunosQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        var escopo = EscopoAlunos.Para(usuario);
        var filtro = new FiltroAlunos(query.Busca, query.CursoId, query.ProfissionalId, query.Situacao,
            query.InicioDe, query.InicioAte, query.Pagina, query.Tamanho, query.EspecialidadeEncaminhamento);
        return await consultas.ListarAsync(filtro, escopo, ct);
    }
}
