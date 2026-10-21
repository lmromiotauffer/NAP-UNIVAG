using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Nap.Api.Infra;

/// <summary>Esquema <c>Bearer</c> (JWT) e requisito global no documento OpenAPI.</summary>
public sealed class OpenApiBearerTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument documento, OpenApiDocumentTransformerContext contexto, CancellationToken ct)
    {
        documento.Components ??= new OpenApiComponents();
        documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        documento.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
        };
        documento.Security ??= [];
        documento.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", documento)] = [] });
        return Task.CompletedTask;
    }
}
