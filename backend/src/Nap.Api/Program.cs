using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nap.Api.Infra;
using Nap.Application.Comum;
using Nap.Infrastructure;
using Nap.Infrastructure.Arquivos;
using Nap.Infrastructure.Identidade;
using Nap.Infrastructure.Notificacoes;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Seed;
using StackExchange.Redis;

// Sem flags, a inicialização não toca SQL nem Redis: a geração do OpenAPI no build executa este entry point.
// As flags saem de args antes do builder (senão "--migrar --semear" vira a chave "migrar" com valor "--semear").
string[] flags = ["--migrar", "--semear"];
var migrar = args.Contains("--migrar");
var semear = args.Contains("--semear");
// --criar-admin <email> "<nome>": cria o primeiro administrador, imprime a senha temporária e encerra (PrimeiroAdministrador).
var posicaoAdmin = Array.IndexOf(args, "--criar-admin");
if (posicaoAdmin >= 0 && posicaoAdmin + 2 >= args.Length)
    throw new InvalidOperationException("Uso: --criar-admin <email> \"<nome completo>\"");
var argsHost = args.Where((a, i) => !flags.Contains(a) && (posicaoAdmin < 0 || i < posicaoAdmin || i > posicaoAdmin + 2)).ToArray();
var builder = WebApplication.CreateBuilder(argsHost);

// A geração do OpenAPI no build (GetDocument.Insider) inicia o host sem segredos; Jwt:Chave e a string do Redis são
// exigidas na inicialização. Recebe valores fictícios, só nesse processo, que nunca atende requisições nem abre o Redis.
var gerandoOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (gerandoOpenApi)
{
    builder.Configuration["Jwt:Chave"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    builder.Configuration["ConnectionStrings:Redis"] = "localhost";
}

builder.Services.AdicionarApplication();
// Mensagens padrão do FluentValidation (regras sem WithMessage) em português.
ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("pt-BR");
builder.Services.AdicionarInfrastructure(builder.Configuration);
// A geração do OpenAPI no build não pode varrer a outbox (tocaria SQL).
if (!gerandoOpenApi)
    builder.Services.AddHostedService<ProcessadorOutbox>();

builder.Services.AddControllers(o =>
    {
        // Campos obrigatórios são validados pelos validators da Application (mensagens e chaves por campo).
        o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        ManipuladorExcecoes.MensagensEmPortugues(o.ModelBindingMessageProvider);
    })
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        o.AllowInputFormatterExceptionMessages = false; // mensagens do System.Text.Json são em inglês e expõem detalhes
    })
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ManipuladorExcecoes.ModeloInvalido);
// O gerador de OpenAPI lê estas opções: enums como string e números só como número (sem "string" + pattern) no documento.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

builder.Services.AddOptions<OpcoesLimiteLogin>().Bind(builder.Configuration.GetSection("Nap:LimiteLogin"))
    .Validate(o => o.Tentativas > 0 && o.JanelaSegundos > 0, "Nap:LimiteLogin: Tentativas e JanelaSegundos devem ser maiores que zero.")
    .ValidateOnStart();
builder.Services.AddRateLimiter(LimiteLogin.Configurar);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManipuladorExcecoes>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
// Lido na primeira requisição, não no build: a chave vem de segredo do ambiente.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<OpcoesJwt>>((o, opcoesJwt) =>
    {
        var jwt = opcoesJwt.Value;
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Emissor,
            ValidAudience = jwt.Audiencia,
            IssuerSigningKey = jwt.ChaveDeAssinatura(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ServicoTokens.ClaimNome,
            RoleClaimType = ServicoTokens.ClaimPerfil,
        };
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = async contexto =>
            {
                var principal = contexto.Principal;
                if (!Guid.TryParse(principal?.FindFirst(ServicoTokens.ClaimId)?.Value, out var usuarioId)
                    || principal.FindFirst(ServicoTokens.ClaimCarimbo)?.Value is not { Length: > 0 } carimbo
                    || !Guid.TryParse(principal.FindFirst(ServicoTokens.ClaimSessao)?.Value, out var familiaId))
                {
                    contexto.Fail("Token sem identificação de sessão");
                    return;
                }
                var validador = contexto.HttpContext.RequestServices.GetRequiredService<ValidadorCarimboSeguranca>();
                if (!await validador.ValidarAsync(usuarioId, carimbo, familiaId, contexto.HttpContext.RequestAborted))
                    contexto.Fail("Sessão revogada");
            },
            OnChallenge = contexto =>
            {
                contexto.HandleResponse();
                return ManipuladorExcecoes.EscreverAsync(contexto.HttpContext, 401, new UnauthorizedException().Message);
            },
            OnForbidden = contexto => ManipuladorExcecoes.EscreverAsync(contexto.HttpContext, 403, new ForbiddenException().Message),
        };
    });
builder.Services.AddAuthorization(Politicas.Configurar);
// Substitui o padrão (um único registro): audita o 403 por política e delega a ele.
builder.Services.Replace(ServiceDescriptor.Singleton<IAuthorizationMiddlewareResultHandler, AuditoriaAutorizacao>());

builder.Services.AddOpenApi(o =>
{
    o.AddDocumentTransformer<OpenApiBearerTransformer>();
    o.AddOperationTransformer<OpenApiOperacaoTransformer>();
    o.AddSchemaTransformer<OpenApiEnumTransformer>();
});

builder.Services.AddHealthChecks()
    .Add(new HealthCheckRegistration("sql",
        // Banco sem as migrações (API iniciada sem --migrar) conecta, mas toda requisição daria 500.
        sp => new SondaSaude(async ct =>
        {
            var banco = sp.GetRequiredService<NapDbContext>().Database;
            return await banco.CanConnectAsync(ct) && !(await banco.GetPendingMigrationsAsync(ct)).Any();
        }), null, ["ready"]))
    .Add(new HealthCheckRegistration("redis",
        sp => new SondaSaude(async _ => { await sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase().PingAsync(); return true; }),
        HealthStatus.Degraded, ["ready"]));

var app = builder.Build();

if (semear && !app.Environment.IsDevelopment())
    throw new InvalidOperationException("--semear só pode ser usado no ambiente Development.");
if (migrar || semear)
{
    await using var escopo = app.Services.CreateAsyncScope();
    if (migrar)
        await escopo.ServiceProvider.GetRequiredService<NapDbContext>().Database.MigrateAsync();
    if (semear)
        await escopo.ServiceProvider.GetRequiredService<SemeadorDemonstracao>().ExecutarAsync();
}
if (posicaoAdmin >= 0)
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    await using var escopo = app.Services.CreateAsyncScope();
    try
    {
        var senha = await PrimeiroAdministrador.CriarAsync(escopo.ServiceProvider, args[posicaoAdmin + 1], args[posicaoAdmin + 2]);
        // Console, nunca logger: a senha não pode ir para o destino de logs.
        Console.WriteLine($"Administrador criado. Senha temporária (exibida só agora; troca obrigatória no primeiro acesso): {senha}");
    }
    catch (InvalidOperationException e)
    {
        Console.Error.WriteLine($"Administrador não criado: {e.Message}");
        Environment.ExitCode = 1;
    }
    return;
}
if (!gerandoOpenApi && app.Services.GetRequiredService<IArmazenamentoArquivos>() is ArmazenamentoEmDisco disco)
    disco.VerificarGravacao();

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.Use((contexto, proximo) =>
{
    var cabecalhos = contexto.Response.Headers;
    cabecalhos.XContentTypeOptions = "nosniff";
    cabecalhos.XFrameOptions = "DENY";
    cabecalhos.ContentSecurityPolicy = "frame-ancestors 'none'";
    return proximo(contexto);
});
// Produção: o build do frontend (frontend/dist) vai para wwwroot e é servido na mesma origem da API (o cookie de refresh
// é SameSite=Strict). index.html sem cache; assets com hash no nome, imutáveis.
var opcoesEstaticos = new StaticFileOptions
{
    OnPrepareResponse = c => c.Context.Response.Headers.CacheControl =
        c.Context.Request.Path.StartsWithSegments("/assets") ? "public, max-age=31536000, immutable" : "no-cache",
};
app.UseDefaultFiles(); // "/" → index.html (o fallback abaixo não casa o caminho vazio)
app.UseStaticFiles(opcoesEstaticos);
// Roteamento explícito depois dos estáticos: com o UseRouting implícito no início, o fallback do SPA já estaria escolhido e
// o UseStaticFiles (que não serve quando há endpoint) devolveria index.html no lugar dos assets.
app.UseRouting();
// Depois do roteamento, exigido pelo [EnableRateLimiting].
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExigeTrocaDeSenha>();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();
// Rotas do SPA (inclusive /assinar/:token, sem login) caem no index.html; /api e /health inexistentes continuam fora.
app.MapFallbackToFile("{*caminho:regex(^(?!api(/|$)|health(/|$)).*$)}", "index.html", opcoesEstaticos).AllowAnonymous();

app.Run();

public partial class Program;
