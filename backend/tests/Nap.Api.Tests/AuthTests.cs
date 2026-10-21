using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nap.Domain.Comum;
using Nap.Infrastructure.Identidade;

namespace Nap.Api.Tests;

public class AuthTests(ApiFactory fabrica)
{
    private static HttpRequestMessage ComCabecalhoSpa(string caminho) =>
        new(HttpMethod.Post, caminho) { Headers = { { "X-Requested-With", "nap-spa" } } };

    [Fact]
    public async Task Refresh_via_cookie_rotaciona_e_logout_revoga()
    {
        var cliente = ApiFactory.NovoCliente(fabrica);
        var login = await cliente.PostAsJsonAsync("/api/auth/login", new { email = "coordenacao@nap.demo", senha = ApiFactory.SenhaPadrao });
        login.EnsureSuccessStatusCode();
        var tokenLogin = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var cookieLogin = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("nap_refresh="));
        Assert.Contains("secure", cookieLogin, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookieLogin, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookieLogin, StringComparison.OrdinalIgnoreCase);

        // sem o cabeçalho anti-CSRF: 400, mesmo com o cookie
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsync("/api/auth/refresh", null)).StatusCode);

        var refresh = await cliente.SendAsync(ComCabecalhoSpa("/api/auth/refresh"));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var corpo = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        var tokenNovo = corpo.GetProperty("accessToken").GetString()!;
        Assert.NotEqual(tokenLogin, tokenNovo);
        var cookieNovo = refresh.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("nap_refresh="));
        Assert.NotEqual(cookieLogin.Split(';')[0], cookieNovo.Split(';')[0]); // rotação
        Assert.DoesNotContain("refresh", corpo.ToString(), StringComparison.OrdinalIgnoreCase); // refresh só no cookie

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenNovo);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/auth/eu")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(ComCabecalhoSpa("/api/auth/logout"))).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.SendAsync(ComCabecalhoSpa("/api/auth/refresh"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/auth/eu")).StatusCode); // sessão revogada
    }

    [Fact]
    public async Task Token_antigo_deixa_de_valer_apos_mudanca_de_stamp()
    {
        // usuário próprio: trocar o stamp de um usuário do seed derrubaria os outros testes em paralelo
        var email = $"stamp.{Guid.NewGuid():N}@nap.teste";
        Guid id;
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentidade>>();
            var usuario = new UsuarioIdentidade
            {
                Id = Guid.CreateVersion7(), UserName = email, Email = email, EmailConfirmed = true,
                NomeCompleto = "Usuário de teste", Perfil = Perfis.CoordenadorNap, Ativo = true,
            };
            Assert.True((await usuarios.CreateAsync(usuario, ApiFactory.SenhaPadrao)).Succeeded);
            id = usuario.Id;
        }

        var cliente = await ApiFactory.ClienteComo(fabrica, email);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/auth/eu")).StatusCode); // preenche o cache do carimbo

        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentidade>>();
            Assert.True((await usuarios.UpdateSecurityStampAsync((await usuarios.FindByIdAsync(id.ToString()))!)).Succeeded);
            await escopo.ServiceProvider.GetRequiredService<IDistributedCache>().RemoveAsync(ValidadorCarimboSeguranca.Chave(id));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/auth/eu")).StatusCode);
    }

    [Fact]
    public async Task Login_acima_do_limite_da_janela_responde_429()
    {
        // Host próprio com o limite de produção (a fábrica usa um limite alto): estado do limitador isolado dos outros testes.
        await using var limitado = fabrica.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Nap:LimiteLogin:Tentativas"] = "10",
                ["Nap:LimiteLogin:JanelaSegundos"] = "60",
            })));
        var cliente = ApiFactory.NovoCliente(limitado);
        var tentativa = new { email = $"limite.{Guid.NewGuid():N}@nap.teste", senha = "SenhaErrada123" };

        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/auth/login", tentativa)).StatusCode);

        var excedente = await cliente.PostAsJsonAsync("/api/auth/login", tentativa);
        Assert.Equal(HttpStatusCode.TooManyRequests, excedente.StatusCode);
        Assert.Equal("application/problem+json", excedente.Content.Headers.ContentType?.MediaType);
        Assert.True(excedente.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        var problema = await excedente.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(429, problema.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problema.GetProperty("title").GetString()));
    }
}
