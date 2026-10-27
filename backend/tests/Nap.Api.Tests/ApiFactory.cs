using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Seed;
using Testcontainers.MsSql;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(Nap.Api.Tests.ApiFactory))]

namespace Nap.Api.Tests;

/// <summary>
/// Api real em memória sobre SQL Server e Redis reais (Testcontainers), com migrações e seed aplicados uma vez por execução.
/// Config por AddInMemoryCollection (última fonte): vence user-secrets e variáveis de ambiente do desenvolvedor.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Se o Testcontainers não achar o Docker no Windows: DOCKER_HOST=npipe:////./pipe/dockerDesktopLinuxEngine
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    private string _stringSql = null!;

    public const string SenhaPadrao = "SenhaTeste123";
    public static readonly Uri Endereco = new("https://localhost");

    /// <summary>Diretório de arquivos desta execução; apagado no fim.</summary>
    public string DiretorioArquivos { get; } = Path.Combine(Path.GetTempPath(), "nap-api-testes-" + Guid.NewGuid().ToString("N"));

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _redis.StartAsync());
        _stringSql = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "NapApiTestes" }.ConnectionString;
        await using var escopo = Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<NapDbContext>().Database.MigrateAsync();
        await escopo.ServiceProvider.GetRequiredService<SemeadorDemonstracao>().ExecutarAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // o semeador recusa Production
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Nap"] = _stringSql,
            ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),
            ["Jwt:Chave"] = "chave-de-teste-com-bem-mais-de-32-bytes-0123456789",
            ["Seed:SenhaPadrao"] = SenhaPadrao,
            ["Nap:Arquivos:Diretorio"] = DiretorioArquivos,
            // Todos os clientes do TestServer caem na mesma partição (sem IP): limite alto para a suíte inteira.
            ["Nap:LimiteLogin:Tentativas"] = "100000",
            ["Nap:LimiteLogin:TentativasAssinatura"] = "100000",
        }));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _redis.DisposeAsync();
        await _sql.DisposeAsync();
        if (Directory.Exists(DiretorioArquivos))
            Directory.Delete(DiretorioArquivos, recursive: true);
    }

    /// <summary>Mesma Api, com o Redis apontando para uma porta fechada.</summary>
    public static WebApplicationFactory<Program> SemRedis(ApiFactory fabrica) =>
        fabrica.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Redis"] = "localhost:1" })));

    /// <summary>Usuário próprio (CoordenadorNap) para testes que mexem em sessão/stamp sem afetar os do seed.</summary>
    public static async Task<(string Email, Guid Id)> CriarUsuarioAsync(WebApplicationFactory<Program> fabrica)
    {
        var email = $"teste.{Guid.NewGuid():N}@nap.teste";
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Nap.Infrastructure.Identidade.UsuarioIdentidade>>();
        var usuario = new Nap.Infrastructure.Identidade.UsuarioIdentidade
        {
            Id = Guid.CreateVersion7(), UserName = email, Email = email, EmailConfirmed = true,
            NomeCompleto = "Usuário de teste", Perfil = Nap.Domain.Comum.Perfis.CoordenadorNap, Ativo = true,
        };
        Assert.True((await usuarios.CreateAsync(usuario, SenhaPadrao)).Succeeded);
        return (email, usuario.Id);
    }

    public static HttpClient NovoCliente(WebApplicationFactory<Program> fabrica) =>
        fabrica.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = Endereco, HandleCookies = true, AllowAutoRedirect = false });

    /// <summary>Login e Bearer definido; o cookie de refresh fica no próprio cliente.</summary>
    public Task<HttpClient> ClienteComo(string email) => ClienteComo(this, email);

    public static async Task<HttpClient> ClienteComo(WebApplicationFactory<Program> fabrica, string email, string senha = SenhaPadrao)
    {
        var cliente = NovoCliente(fabrica);
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new { email, senha });
        resposta.EnsureSuccessStatusCode();
        var sessao = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.GetProperty("accessToken").GetString());
        return cliente;
    }

    public static async Task<Guid> IdDoUsuarioAsync(HttpClient cliente) =>
        (await cliente.GetFromJsonAsync<JsonElement>("/api/auth/eu")).GetProperty("id").GetGuid();

    /// <summary>Cria um aluno novo (matrícula única) no primeiro curso, com <paramref name="profissionalId"/> responsável.</summary>
    public static async Task<(Guid Id, string Matricula)> CriarAlunoAsync(HttpClient cliente, Guid profissionalId, string? nomeCurso = null)
    {
        var cursos = await cliente.GetFromJsonAsync<JsonElement>("/api/cursos");
        var curso = nomeCurso is null ? cursos.EnumerateArray().First()
            : cursos.EnumerateArray().Single(c => c.GetProperty("nome").GetString() == nomeCurso);
        var cursoId = curso.GetProperty("id").GetGuid();
        var matricula = "T" + Guid.NewGuid().ToString("N")[..10];
        var resposta = await cliente.PostAsJsonAsync("/api/alunos", new
        {
            nomeCompleto = "Aluno de Teste", matricula, cursoId, semestre = 1,
            inicioAcompanhamento = "2026-02-01", profissionalResponsavelId = profissionalId,
        });
        resposta.EnsureSuccessStatusCode();
        return ((await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(), matricula);
    }
}
