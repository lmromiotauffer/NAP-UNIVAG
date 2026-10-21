using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Domain.Comum;

namespace Nap.Api.Infra;

/// <summary>
/// Exceções → ProblemDetails (spec §4.11). O 500 é genérico: sem stack, SQL ou conteúdo clínico na resposta; a exceção vai só
/// para o log (sem corpo da requisição). Em .NET 10 o middleware não registra exceções tratadas aqui, por isso o log é nosso.
/// </summary>
public sealed class ManipuladorExcecoes(ILogger<ManipuladorExcecoes> logger) : IExceptionHandler
{
    public const string ErroInesperado = "Ocorreu um erro inesperado";

    public const int ClienteDesconectado = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception excecao, CancellationToken ct)
    {
        // Cliente desconectou: não é erro do servidor nem há para quem escrever o corpo.
        if (excecao is OperationCanceledException or IOException && http.RequestAborted.IsCancellationRequested)
        {
            http.Response.StatusCode = ClienteDesconectado;
            return true;
        }

        var (status, titulo) = excecao switch
        {
            ValidationException => (400, excecao.Message),
            UnauthorizedException => (401, excecao.Message),
            ForbiddenException => (403, excecao.Message),
            NotFoundException => (404, excecao.Message),
            ConflictException => (409, excecao.Message),
            DomainException => (422, excecao.Message),
            BadHttpRequestException b => (b.StatusCode, "Requisição inválida."),
            _ => (500, ErroInesperado),
        };
        if (status == 500)
            logger.LogError(excecao, "Erro inesperado em {Metodo} {Caminho}", http.Request.Method, http.Request.Path);
        if (status == 403 && http.User.Identity?.IsAuthenticated == true)
        {
            // O middleware limpa endpoint e rota do contexto antes de chamar este manipulador; ficam na feature.
            var origem = http.Features.Get<IExceptionHandlerFeature>();
            await AuditoriaAutorizacao.RegistrarAcessoNegadoAsync(http, origem?.Endpoint, origem?.RouteValues);
        }

        await EscreverAsync(http, status, titulo, (excecao as ValidationException)?.Erros, (excecao as ConflitoAgendaException)?.Conflitos);
        return true;
    }

    /// <summary>
    /// Escreve ProblemDetails (type + traceId pela fábrica do MVC) sempre como <c>application/problem+json</c>, sem negociação:
    /// um <c>Accept</c> restritivo não transforma o 4xx em 500. Usado também pelos eventos 401/403 do JwtBearer.
    /// </summary>
    public static Task EscreverAsync(HttpContext http, int status, string titulo, IDictionary<string, string[]>? erros = null,
        IReadOnlyList<ConflitoAgendaDto>? conflitos = null)
    {
        var fabrica = http.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        ProblemDetails problema;
        if (erros is null)
            problema = fabrica.CreateProblemDetails(http, status, titulo);
        else
        {
            var validacao = fabrica.CreateValidationProblemDetails(http, new ModelStateDictionary(), status, titulo);
            foreach (var (campo, mensagens) in erros) validacao.Errors[campo] = mensagens;
            problema = validacao;
        }
        if (conflitos is not null) problema.Extensions["conflitos"] = conflitos;
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(problema, problema.GetType(), (JsonSerializerOptions?)null, "application/problem+json");
    }

    /// <summary>Falha de model binding (JSON malformado, tipo errado, corpo ausente) → 400 com <c>errors</c> em camelCase e pt-BR.</summary>
    public static IActionResult ModeloInvalido(ActionContext contexto)
    {
        var problema = contexto.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(contexto.HttpContext, new ModelStateDictionary(), 400, "Há campos inválidos.");
        foreach (var (chave, entrada) in contexto.ModelState)
        {
            if (entrada.Errors.Count == 0) continue;
            var campo = chave.TrimStart('$', '.');
            campo = campo.Length == 0 ? "corpo" : JsonNamingPolicy.CamelCase.ConvertName(campo);
            problema.Errors[campo] = [.. entrada.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Valor inválido." : e.ErrorMessage)];
        }
        return new BadRequestObjectResult(problema) { ContentTypes = { "application/problem+json" } };
    }

    public static void MensagensEmPortugues(DefaultModelBindingMessageProvider m)
    {
        m.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"O valor '{valor}' não é válido para {campo}.");
        m.SetMissingBindRequiredValueAccessor(campo => $"Informe {campo}.");
        m.SetMissingKeyOrValueAccessor(() => "Informe um valor.");
        m.SetMissingRequestBodyRequiredValueAccessor(() => "O corpo da requisição é obrigatório.");
        m.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => $"O valor '{valor}' não é válido.");
        m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Valor inválido.");
        m.SetNonPropertyValueMustBeANumberAccessor(() => "O valor deve ser um número.");
        m.SetUnknownValueIsInvalidAccessor(campo => $"Valor inválido para {campo}.");
        m.SetValueIsInvalidAccessor(valor => $"O valor '{valor}' é inválido.");
        m.SetValueMustBeANumberAccessor(campo => $"{campo} deve ser um número.");
        m.SetValueMustNotBeNullAccessor(valor => $"Informe um valor para {valor}.");
    }
}
