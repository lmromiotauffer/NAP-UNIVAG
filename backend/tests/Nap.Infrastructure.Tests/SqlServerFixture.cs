using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nap.Application.Comum;
using Nap.Application.Features.Configuracoes;
using Nap.Domain.Comum;
using Nap.Domain.Cursos;
using Nap.Infrastructure.Comum;
using Nap.Infrastructure.Identidade;
using Nap.Infrastructure.Persistencia;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(Nap.Infrastructure.Tests.SqlServerFixture))]

namespace Nap.Infrastructure.Tests;

/// <summary>Um SQL Server real por execução; cada teste usa dados próprios (Guids novos), então as classes rodam em paralelo.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Se o Testcontainers não achar o Docker no Windows: DOCKER_HOST=npipe:////./pipe/dockerDesktopLinuxEngine
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = "NapTestes" }.ConnectionString;
        await using var db = CriarContexto();
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>Sempre um contexto novo sobre o mesmo banco.</summary>
    public NapDbContext CriarContexto(Action<string>? log = null)
    {
        var opcoes = new DbContextOptionsBuilder<NapDbContext>().UseSqlServer(ConnectionString);
        if (log is not null)
            opcoes.LogTo(log, [DbLoggerCategory.Database.Command.Name], LogLevel.Information);
        return new NapDbContext(opcoes.Options);
    }

    public static Relogio Relogio() => new();
    /// <summary>Limiares de <c>ConfiguracaoAlertas.Padrao()</c>.</summary>
    public static readonly ConfiguracaoAlertasDto AlertasPadrao = new(3, 3, 30, 7, 15, 15, "");

    public async Task<Guid> CriarProfissionalAsync(string perfil = Perfis.ProfissionalNap, string? nome = null)
    {
        var id = Guid.NewGuid();
        await using var db = CriarContexto();
        db.Users.Add(new UsuarioIdentidade
        {
            Id = id, UserName = id.ToString("N"), NomeCompleto = nome ?? $"Profissional {id:N}", Perfil = perfil, Ativo = true,
        });
        await db.SaveChangesAsync();
        return id;
    }

    public async Task<Guid> CriarCursoAsync(string? nome = null)
    {
        var curso = Curso.Criar(nome ?? $"Curso {Guid.NewGuid():N}"); // nome único: o índice de Curso.Nome não admite repetição
        await using var db = CriarContexto();
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();
        return curso.Id;
    }
}

public sealed class UsuarioAtualTeste(Guid id, string perfil = Perfis.ProfissionalNap, Guid? cursoId = null) : IUsuarioAtual
{
    public Guid Id { get; } = id;
    public string Perfil { get; } = perfil;
    public Guid? CursoId { get; } = cursoId;
}

public sealed class CacheNulo : ICacheConsultas
{
    public Task<T> ObterOuCriarAsync<T>(string escopo, object filtros, Func<CancellationToken, Task<T>> fonte, CancellationToken ct) =>
        fonte(ct);

    public Task InvalidarAreaAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Relógio controlável no fuso institucional.</summary>
public sealed class RelogioTeste(DateTimeOffset agora) : IRelogio
{
    public DateTimeOffset Agora { get; set; } = agora;
    public TimeZoneInfo Fuso { get; } = TimeZoneInfo.FindSystemTimeZoneById(Relogio.FusoInstitucional);
    public DateOnly Hoje => Fuso.DataLocal(Agora);
}

/// <summary>Auditoria montada sem DI, para handlers testados à mão: sem escopo para o contexto próprio, um negado só seria logado.</summary>
public static class AuditoriaTeste
{
    public static Auditoria Criar(NapDbContext db, IUsuarioAtual usuario, IRelogio relogio) =>
        new(db, usuario, relogio, new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<Auditoria>.Instance);
}
