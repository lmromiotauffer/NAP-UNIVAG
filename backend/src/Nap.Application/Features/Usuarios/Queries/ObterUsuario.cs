using Nap.Application.Comum;

namespace Nap.Application.Features.Usuarios.Queries;

public record ObterUsuarioQuery(Guid Id);

public sealed class ObterUsuarioHandler(IGestaoUsuarios gestao, IUsuarioAtual usuario)
{
    public async Task<UsuarioAdminDto> Handle(ObterUsuarioQuery query, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        return await gestao.ObterAsync(query.Id, ct) ?? throw new NotFoundException("Usuário");
    }
}
