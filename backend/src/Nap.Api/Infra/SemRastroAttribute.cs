using Microsoft.AspNetCore.Mvc.Filters;

namespace Nap.Api.Infra;

/// <summary>
/// <c>Cache-Control: no-store</c> e <c>Referrer-Policy: no-referrer</c> em toda resposta, inclusive 400/404. Filtro de recurso
/// com <c>OnStarting</c>: roda antes do model binding, e o manipulador de exceções limpa os cabeçalhos mas não os callbacks
/// (que rodam em ordem inversa, então este, registrado primeiro, vale por último).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SemRastroAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext contexto)
    {
        var resposta = contexto.HttpContext.Response;
        resposta.OnStarting(() =>
        {
            resposta.Headers.CacheControl = "no-store";
            resposta.Headers["Referrer-Policy"] = "no-referrer";
            return Task.CompletedTask;
        });
    }

    public void OnResourceExecuted(ResourceExecutedContext contexto) { }
}
