using Microsoft.AspNetCore.Authorization;
using Nap.Application.Features.Auth;

namespace Nap.Api.Infra;

/// <summary>
/// Com a troca de senha pendente (claim <c>trocar_senha</c>), só a troca, <c>eu</c>, logout e refresh passam; o resto
/// autenticado → 403. Registrado depois de <c>UseAuthorization</c>; endpoints anônimos (login, assinatura, health) não mudam.
/// </summary>
public sealed class ExigeTrocaDeSenha(RequestDelegate proximo)
{
    public const string Mensagem = "Defina uma nova senha para continuar.";

    private static readonly (string Metodo, string Caminho)[] Liberadas =
    [
        (HttpMethods.Post, "/api/auth/senha"),
        (HttpMethods.Get, "/api/auth/eu"),
        (HttpMethods.Post, "/api/auth/logout"),
        (HttpMethods.Post, "/api/auth/refresh"),
    ];

    public Task InvokeAsync(HttpContext http)
    {
        if (http.User.Identity?.IsAuthenticated != true
            || http.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || !http.User.HasClaim(ClaimsNap.TrocarSenha, "1")
            || Liberadas.Any(r => HttpMethods.Equals(r.Metodo, http.Request.Method)
                && http.Request.Path.Equals(r.Caminho, StringComparison.OrdinalIgnoreCase)))
            return proximo(http);
        return ManipuladorExcecoes.EscreverAsync(http, 403, Mensagem);
    }
}
