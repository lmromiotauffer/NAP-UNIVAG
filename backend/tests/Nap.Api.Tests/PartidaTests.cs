using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nap.Api.Infra;

namespace Nap.Api.Tests;

public class PartidaTests(ApiFactory fabrica)
{
    [Fact]
    public async Task Criar_admin_recusa_quando_ja_existe_administrador_ativo()
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PrimeiroAdministrador.CriarAsync(escopo.ServiceProvider, "novo.admin@nap.teste", "Novo Administrador", TestContext.Current.CancellationToken));
        Assert.Contains("Já existe administrador ativo", erro.Message);
    }

    [Fact]
    public void Sem_a_connection_string_do_redis_a_api_recusa_partir()
    {
        // Sem a string, o cache de consultas não resolveria e toda escrita daria 500 com /health/ready em Degraded.
        using var sem = fabrica.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Redis"] = null })));
        var erro = Assert.Throws<InvalidOperationException>(() => sem.Server);
        Assert.Contains("Redis", erro.Message);
    }

    [Fact]
    public async Task Cabecalhos_de_seguranca_e_api_inexistente_fora_do_fallback_do_spa()
    {
        var cliente = ApiFactory.NovoCliente(fabrica);
        var resposta = await cliente.GetAsync("/health/live", TestContext.Current.CancellationToken);
        Assert.Equal("nosniff", resposta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", resposta.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("frame-ancestors 'none'", resposta.Headers.GetValues("Content-Security-Policy").Single());

        // /api inexistente não pode virar index.html (200 HTML onde o frontend espera JSON).
        var api = await cliente.GetAsync("/api/rota-que-nao-existe", TestContext.Current.CancellationToken);
        Assert.NotEqual(HttpStatusCode.OK, api.StatusCode);
        Assert.NotEqual("text/html", api.Content.Headers.ContentType?.MediaType);
    }
}
