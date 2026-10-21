using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace Nap.Api.Infra;

/// <summary>
/// Completa cada operação do contrato: respostas de erro ProblemDetails, só <c>application/json</c>, headers
/// <c>Idempotency-Key</c>/<c>X-Requested-With</c> e endpoints anônimos sem o requisito Bearer.
/// </summary>
public sealed class OpenApiOperacaoTransformer : IOpenApiOperationTransformer
{
    private const string Json = "application/json";
    private const string Problema = "application/problem+json";

    public async Task TransformAsync(OpenApiOperation operacao, OpenApiOperationTransformerContext contexto, CancellationToken ct)
    {
        var descricao = contexto.Description;
        var metadados = descricao.ActionDescriptor.EndpointMetadata;
        var caminho = descricao.RelativePath ?? "";
        var escrita = descricao.HttpMethod != "GET";
        var anonimo = metadados.OfType<IAllowAnonymous>().Any();
        var idempotente = metadados.OfType<IdempotenteAttribute>().Any();
        var exigeIfMatch = metadados.OfType<ExigeIfMatchAttribute>().Any();
        var limitado = metadados.OfType<EnableRateLimitingAttribute>().Any();
        var sessaoPorCookie = caminho is "api/auth/refresh" or "api/auth/logout";

        SoJson(operacao.RequestBody?.Content);
        foreach (var resposta in operacao.Responses?.Values.ToList() ?? [])
            SoJson(resposta.Content);

        if (anonimo)
            operacao.Security = [];
        if (idempotente)
            Cabecalho(operacao, IdempotenteAttribute.Cabecalho, false, new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 100 });
        if (exigeIfMatch)
            Cabecalho(operacao, ExigeIfMatchAttribute.Cabecalho, true, new OpenApiSchema { Type = JsonSchemaType.String });
        if (sessaoPorCookie)
            Cabecalho(operacao, "X-Requested-With", true, new OpenApiSchema { Type = JsonSchemaType.String, Enum = [JsonValue.Create("nap-spa")] });

        if (operacao.RequestBody is not null || operacao.Parameters is { Count: > 0 })
            await Erro(operacao, contexto, 400, "Requisição inválida", typeof(ValidationProblemDetails), ct);
        if (!anonimo || caminho is "api/auth/login" or "api/auth/refresh")
            await Erro(operacao, contexto, 401, "Não autenticado", typeof(ProblemDetails), ct);
        if (!anonimo)
            await Erro(operacao, contexto, 403, "Sem permissão", typeof(ProblemDetails), ct);
        // As rotas anônimas do aluno respondem 404 genérico a link inválido, sem parâmetro de caminho.
        if (caminho.Contains("{id") || caminho.Contains("{alunoId") || caminho.Contains("{peiId") || caminho.StartsWith("api/assinar/"))
            await Erro(operacao, contexto, 404, "Não encontrado", typeof(ProblemDetails), ct);
        // Escritas em aluno existente (PUT, /situacao, /transferencia) também dão 409 por conflito de rowversion.
        if (idempotente || (escrita && (caminho.StartsWith("api/agenda") || caminho.StartsWith("api/atendimentos") || caminho.StartsWith("api/atividades") || caminho.StartsWith("api/producao")
                || caminho.StartsWith("api/alunos/{id") || caminho.StartsWith("api/usuarios") || caminho.StartsWith("api/cursos")
                || caminho.StartsWith("api/escalas") || caminho.StartsWith("api/feriados") || caminho.StartsWith("api/configuracoes")
                || caminho.StartsWith("api/peis/{id")
                || caminho.StartsWith("api/encaminhamentos/{id") || caminho.StartsWith("api/comunicacoes/rascunhos/{id")
                || caminho.StartsWith("api/comunicacoes/{id") || caminho.StartsWith("api/documentos/{id")
                || caminho.StartsWith("api/peis/versoes/{id") || caminho == "api/assinar/assinar")))
            await Erro(operacao, contexto, 409, "Conflito",
                metadados.OfType<ProducesResponseTypeAttribute>().FirstOrDefault(a => a.StatusCode == 409)?.Type ?? typeof(ProblemDetails), ct);
        if (exigeIfMatch)
            await Erro(operacao, contexto, 428, "Versão do registro ausente", typeof(ProblemDetails), ct);
        if (escrita && !caminho.StartsWith("api/auth"))
            await Erro(operacao, contexto, 422, "Regra de negócio violada", typeof(ProblemDetails), ct);
        if (limitado)
            await Erro(operacao, contexto, 429, "Muitas tentativas", typeof(ProblemDetails), ct);
    }

    private static void SoJson(IDictionary<string, OpenApiMediaType>? conteudo)
    {
        if (conteudo is null || !conteudo.ContainsKey(Json)) return;
        foreach (var tipo in conteudo.Keys.Where(k => k != Json).ToList())
            conteudo.Remove(tipo);
    }

    private static void Cabecalho(OpenApiOperation operacao, string nome, bool obrigatorio, OpenApiSchema esquema)
    {
        operacao.Parameters ??= [];
        operacao.Parameters.Add(new OpenApiParameter { Name = nome, In = ParameterLocation.Header, Required = obrigatorio, Schema = esquema });
    }

    private static async Task Erro(OpenApiOperation operacao, OpenApiOperationTransformerContext contexto, int status, string descricao,
        Type tipo, CancellationToken ct)
    {
        var documento = contexto.Document!;
        if (documento.Components?.Schemas?.ContainsKey(tipo.Name) != true)
            documento.AddComponent(tipo.Name, await contexto.GetOrCreateSchemaAsync(tipo, null, ct));
        operacao.Responses ??= [];
        operacao.Responses[status.ToString()] = new OpenApiResponse
        {
            Description = descricao,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [Problema] = new() { Schema = new OpenApiSchemaReference(tipo.Name, documento) },
            },
        };
    }
}

/// <summary>Enums: <c>type: string</c> e sem <c>null</c> na lista compartilhada. O uso anulável continua anulável no esquema
/// da propriedade (<c>oneOf: [null, $ref]</c>).</summary>
public sealed class OpenApiEnumTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema esquema, OpenApiSchemaTransformerContext contexto, CancellationToken ct)
    {
        var tipo = contexto.JsonTypeInfo.Type;
        if ((Nullable.GetUnderlyingType(tipo) ?? tipo).IsEnum && esquema.Enum is { } valores)
        {
            esquema.Type = JsonSchemaType.String;
            for (var i = valores.Count - 1; i >= 0; i--)
                if (valores[i] is null || valores[i]!.GetValueKind() == System.Text.Json.JsonValueKind.Null)
                    valores.RemoveAt(i);
        }
        return Task.CompletedTask;
    }
}
