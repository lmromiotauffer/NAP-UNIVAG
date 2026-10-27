using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Nap.Api.Infra;

/// <summary>
/// Edições exigem o header <c>If-Match</c> com a versão do registro (campo <c>versao</c> do DTO). Ausente, vazio ou repetido → 428.
/// É filtro de recurso: roda depois da autorização (403 vem antes) e antes do model binding.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExigeIfMatchAttribute : Attribute, IAsyncResourceFilter
{
    public const string Cabecalho = "If-Match";

    public async Task OnResourceExecutionAsync(ResourceExecutingContext contexto, ResourceExecutionDelegate proximo)
    {
        var http = contexto.HttpContext;
        var valores = http.Request.Headers[Cabecalho];
        if (valores.Count != 1 || string.IsNullOrWhiteSpace(valores[0]))
        {
            await ManipuladorExcecoes.EscreverAsync(http, StatusCodes.Status428PreconditionRequired,
                "Envie o cabeçalho If-Match com a versão do registro.");
            contexto.Result = new EmptyResult();
            return;
        }
        await proximo();
    }
}

public static class IfMatchExtensoes
{
    /// <summary>Valor único do <c>If-Match</c> sem as aspas nem o prefixo de ETag fraca <c>W/</c> (o filtro já garantiu que existe).</summary>
    public static string VersaoIfMatch(this HttpContext http)
    {
        var valor = http.Request.Headers[ExigeIfMatchAttribute.Cabecalho].ToString().Trim();
        return (valor.StartsWith("W/", StringComparison.Ordinal) ? valor[2..] : valor).Trim('"');
    }
}
