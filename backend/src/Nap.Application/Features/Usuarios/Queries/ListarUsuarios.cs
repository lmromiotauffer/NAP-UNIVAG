using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Usuarios.Queries;

public record ListarUsuariosQuery(string? Busca = null, string? Perfil = null, Guid? CursoId = null, bool? Ativo = null,
    int Pagina = 1, int Tamanho = 20);

public class ListarUsuariosValidator : AbstractValidator<ListarUsuariosQuery>
{
    public ListarUsuariosValidator()
    {
        RuleFor(x => x.Pagina).InclusiveBetween(1, 100_000);
        RuleFor(x => x.Tamanho).InclusiveBetween(1, 100);
        RuleFor(x => x.Busca).MaximumLength(200);
    }
}

public sealed class ListarUsuariosHandler(IValidator<ListarUsuariosQuery> validador, IGestaoUsuarios gestao, IUsuarioAtual usuario)
{
    public async Task<PagedResult<UsuarioAdminDto>> Handle(ListarUsuariosQuery query, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(query, ct);
        return await gestao.ListarAsync(
            new FiltroUsuarios(query.Busca, query.Perfil, query.CursoId, query.Ativo, query.Pagina, query.Tamanho), ct);
    }
}
