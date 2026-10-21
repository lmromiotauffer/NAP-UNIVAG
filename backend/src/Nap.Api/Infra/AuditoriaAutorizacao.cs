using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Routing.Patterns;
using Nap.Application.Comum;

namespace Nap.Api.Infra;

/// <summary>
/// 403 por política de usuário autenticado → <c>acesso.negado</c> na auditoria; depois segue o tratamento padrão.
/// 401 (desafio) não é registrado.
/// </summary>
public sealed class AuditoriaAutorizacao : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _padrao = new();

    public async Task HandleAsync(RequestDelegate proximo, HttpContext http, AuthorizationPolicy politica, PolicyAuthorizationResult resultado)
    {
        if (resultado.Forbidden && http.User.Identity?.IsAuthenticated == true)
            await RegistrarAcessoNegadoAsync(http, http.GetEndpoint(), http.Request.RouteValues);
        await _padrao.HandleAsync(proximo, http, politica, resultado);
    }

    /// <summary>
    /// Registra <c>acesso.negado</c> com recurso <c>"{MÉTODO} {template da rota}"</c> (ex.: <c>GET api/alunos/{id}</c>, sem restrições,
    /// até 100 caracteres) e recursoId = parâmetro de rota <c>id</c>, quando é Guid. Também usado pelo <see cref="ManipuladorExcecoes"/>.
    /// </summary>
    public static Task RegistrarAcessoNegadoAsync(HttpContext http, Endpoint? endpoint, RouteValueDictionary? rotas)
    {
        var caminho = endpoint is RouteEndpoint rota ? Template(rota.RoutePattern) : http.Request.Path.ToString();
        var recurso = $"{http.Request.Method} {caminho}";
        if (recurso.Length > 100) recurso = recurso[..100];
        Guid? recursoId = Guid.TryParse(rotas?.GetValueOrDefault("id")?.ToString(), out var id) ? id : null;
        // Sem o token da requisição: um cliente que desiste não apaga o rastro.
        return http.RequestServices.GetRequiredService<IAuditoria>().RegistrarNegadoAsync("acesso.negado", recurso, recursoId,
            CancellationToken.None);
    }

    private static string Template(RoutePattern padrao) =>
        string.Join('/', padrao.PathSegments.Select(segmento => string.Concat(segmento.Parts.Select(p => p switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternSeparatorPart separador => separador.Content,
            RoutePatternParameterPart parametro => $"{{{parametro.Name}}}",
            _ => "",
        }))));
}
