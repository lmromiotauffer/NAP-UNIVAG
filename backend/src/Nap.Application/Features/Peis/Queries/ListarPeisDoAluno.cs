using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Peis.Queries;

public record ListarPeisDoAlunoQuery(Guid AlunoId);

public class ListarPeisDoAlunoValidator : AbstractValidator<ListarPeisDoAlunoQuery>
{
    public ListarPeisDoAlunoValidator() => RuleFor(x => x.AlunoId).NotEmpty();
}

public sealed class ListarPeisDoAlunoHandler(IValidator<ListarPeisDoAlunoQuery> validador, IPeiQueries consultas,
    IUsuarioAtual usuario, IRelogio relogio)
{
    public async Task<IReadOnlyList<PeiResumoDto>> Handle(ListarPeisDoAlunoQuery query, CancellationToken ct = default)
    {
        var escopo = EscopoPeis.Para(usuario);
        await validador.ValidarAsync(query, ct);
        var peis = await consultas.ListarDoAlunoAsync(query.AlunoId, escopo, usuario.Id, ct) ?? throw new NotFoundException("Aluno");
        var hoje = relogio.Hoje;
        return peis.Select(p => p with
        {
            Versoes = p.Versoes.Select(v => v with { ForaDaVigencia = VigenciaPei.ForaDaVigencia(v.Estado, v.VigenciaFim, hoje) }).ToList(),
        }).ToList();
    }
}
