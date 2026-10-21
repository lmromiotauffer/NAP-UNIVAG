using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Nap.Application.Comum;

namespace Nap.Infrastructure.Identidade;

/// <summary>
/// Usuário da requisição, lido de <c>HttpContext.User</c> com os nomes de claim emitidos por <see cref="ServicoTokens"/>
/// (sub, role, curso). Depende de a Api validar o JWT com <c>MapInboundClaims = false</c>.
/// Sem usuário autenticado, <see cref="Id"/> lança <see cref="UnauthorizedException"/> (a auditoria conta com isso).
/// </summary>
public sealed class UsuarioAtual(IHttpContextAccessor acessor) : IUsuarioAtual
{
    public Guid Id => Guid.TryParse(Claim(ServicoTokens.ClaimId), out var id) ? id : throw new UnauthorizedException();
    public string Perfil => Claim(ServicoTokens.ClaimPerfil) ?? throw new UnauthorizedException();
    public Guid? CursoId => Guid.TryParse(Claim(ServicoTokens.ClaimCurso), out var curso) ? curso : null;

    private string? Claim(string tipo) =>
        acessor.HttpContext?.User is { Identity.IsAuthenticated: true } usuario ? usuario.FindFirstValue(tipo) : null;
}
