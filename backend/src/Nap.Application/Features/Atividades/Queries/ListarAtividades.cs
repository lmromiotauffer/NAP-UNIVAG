using FluentValidation;
using Nap.Application.Comum;
using Nap.Domain.Atividades;
using Nap.Domain.Comum;

namespace Nap.Application.Features.Atividades.Queries;

public record ListarAtividadesQuery(DateOnly? Inicio = null, DateOnly? Fim = null, Guid? CursoId = null, Guid? ProfissionalId = null,
    TipoAtividade? Tipo = null, bool? Institucionais = null, int Pagina = 1, int Tamanho = 20);

public class ListarAtividadesValidator : AbstractValidator<ListarAtividadesQuery>
{
    public ListarAtividadesValidator()
    {
        RuleFor(x => x.Pagina).InclusiveBetween(1, 100_000);
        RuleFor(x => x.Tamanho).InclusiveBetween(1, 100);
    }
}

public sealed class ListarAtividadesHandler(IValidator<ListarAtividadesQuery> validador, IAtividadeQueries consultas, IUsuarioAtual usuario)
{
    public async Task<PagedResult<AtividadeDto>> Handle(ListarAtividadesQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        var recorte = RecorteDeCurso.Para(usuario);
        // Coordenador de curso só vê o próprio curso: institucionais e filtros de outro curso não ampliam o recorte.
        var institucionais = recorte is null ? query.Institucionais : null;
        return await consultas.ListarAsync(new FiltroAtividades(query.Inicio, query.Fim, query.CursoId, query.ProfissionalId,
            query.Tipo, institucionais, recorte, query.Pagina, query.Tamanho), ct);
    }
}

internal static class RecorteDeCurso
{
    /// <summary>Equipe NAP → sem recorte (null); coordenador de curso → o próprio curso; demais → 403.</summary>
    public static Guid? Para(IUsuarioAtual usuario)
    {
        if (usuario.EhEquipeNap()) return null;
        if (usuario.Perfil == Perfis.CoordenadorCurso) return usuario.CursoId ?? throw new ForbiddenException();
        throw new ForbiddenException();
    }
}
