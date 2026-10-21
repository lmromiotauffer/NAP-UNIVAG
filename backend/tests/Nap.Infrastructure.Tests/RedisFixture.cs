using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(Nap.Infrastructure.Tests.RedisFixture))]

namespace Nap.Infrastructure.Tests;

/// <summary>Um Redis real por execução; os testes usam chaves próprias (Guids novos).</summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>Contêiner de DI montado por <c>AdicionarInfrastructure</c>, validando todos os registros na construção.</summary>
    public static ServiceProvider CriarProvedor(SqlServerFixture sql, RedisFixture redis, string? conexaoSql = null,
        IDictionary<string, string?>? ajustes = null, string ambiente = "Development")
    {
        var dados = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Nap"] = conexaoSql ?? sql.ConnectionString,
            ["ConnectionStrings:Redis"] = redis.ConnectionString,
            ["Jwt:Chave"] = "chave-de-teste-com-mais-de-32-bytes-0123456789",
            ["Seed:SenhaPadrao"] = "SenhaDemo2026",
        };
        foreach (var (chave, valor) in ajustes ?? new Dictionary<string, string?>())
            dados[chave] = valor;
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(dados).Build();
        var servicos = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuracao)
            .AddSingleton<IHostEnvironment>(new AmbienteTeste(ambiente));
        servicos.AdicionarInfrastructure(configuracao);
        return servicos.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>O host real registra o IHostEnvironment; aqui só o nome importa.</summary>
    private sealed class AmbienteTeste(string nome) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nome;
        public string ApplicationName { get; set; } = "Nap.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
