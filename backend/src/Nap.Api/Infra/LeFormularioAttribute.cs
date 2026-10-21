using Microsoft.AspNetCore.Mvc.Filters;
using Nap.Application.Comum;

namespace Nap.Api.Infra;

/// <summary>
/// Lê o formulário antes do model binding. No binding, o MVC engole falhas de leitura (estouro do teto, multipart malformado)
/// como erro genérico em inglês no campo <c>corpo</c>. Aqui o estouro vira o mesmo 400 do limite de negócio no campo
/// <c>arquivo</c> e o formulário inválido, 400 em português no campo <c>corpo</c>. Os filtros de limite rodam antes
/// (autorização); o binding reusa o formulário já lido.
/// </summary>
/// <param name="tetoBytes">O mesmo valor do <c>[RequestSizeLimit]</c> da ação.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LeFormularioAttribute(long tetoBytes) : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext contexto, ResourceExecutionDelegate proximo)
    {
        var http = contexto.HttpContext;
        var requisicao = http.Request;
        if (requisicao.HasFormContentType)
        {
            if (requisicao.ContentLength > tetoBytes)
                throw ExcedeLimite(http);
            try
            {
                await requisicao.ReadFormAsync(http.RequestAborted);
            }
            // Corpo chunked acima do teto: o Kestrel só descobre durante a leitura.
            catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                throw ExcedeLimite(http);
            }
            // Cliente desconectado segue para o 499 do ManipuladorExcecoes.
            catch (Exception e) when (e is InvalidDataException or IOException && !http.RequestAborted.IsCancellationRequested)
            {
                throw Validacao.ErroDeCampo("corpo", "Formulário inválido.");
            }
        }
        await proximo();
    }

    private static ValidationException ExcedeLimite(HttpContext http) =>
        ValidacaoArquivo.ExcedeLimite(http.RequestServices.GetRequiredService<OpcoesArquivos>().LimiteBytes);
}
