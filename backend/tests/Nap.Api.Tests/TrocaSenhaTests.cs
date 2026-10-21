using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nap.Domain.Comum;
using Nap.Infrastructure.Identidade;
using Nap.Infrastructure.Persistencia;

namespace Nap.Api.Tests;

public class TrocaSenhaTests(ApiFactory fabrica)
{
    private const string NovaSenha = "NovaSenha2026x";

    private static HttpRequestMessage ComCabecalhoSpa(string caminho) =>
        new(HttpMethod.Post, caminho) { Headers = { { "X-Requested-With", "nap-spa" } } };

    /// <summary>Usuário próprio (trocar o carimbo de um do seed derrubaria os outros testes em paralelo).</summary>
    private async Task<(string Email, Guid Id)> CriarUsuarioAsync(bool deveTrocar)
    {
        var email = $"troca.{Guid.NewGuid():N}@nap.teste";
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentidade>>();
        var usuario = new UsuarioIdentidade
        {
            Id = Guid.CreateVersion7(), UserName = email, Email = email, EmailConfirmed = true,
            NomeCompleto = "Usuário de teste", Perfil = Perfis.CoordenadorNap, Ativo = true, DeveTrocarSenha = deveTrocar,
        };
        Assert.True((await usuarios.CreateAsync(usuario, ApiFactory.SenhaPadrao)).Succeeded);
        return (email, usuario.Id);
    }

    private static Task<HttpResponseMessage> TrocarAsync(HttpClient cliente, string senhaAtual, string novaSenha) =>
        cliente.PostAsJsonAsync("/api/auth/senha", new { senhaAtual, novaSenha });

    private static async Task<string[]> ErrosAsync(HttpResponseMessage resposta, string campo)
    {
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return [.. corpo.GetProperty("errors").GetProperty(campo).EnumerateArray().Select(e => e.GetString()!)];
    }

    [Fact]
    public async Task Login_com_senha_temporaria_informa_que_deve_trocar()
    {
        var (email, _) = await CriarUsuarioAsync(deveTrocar: true);
        var cliente = ApiFactory.NovoCliente(fabrica);

        var login = await cliente.PostAsJsonAsync("/api/auth/login", new { email, senha = ApiFactory.SenhaPadrao });

        login.EnsureSuccessStatusCode();
        var corpo = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(corpo.GetProperty("usuario").GetProperty("deveTrocarSenha").GetBoolean());
        var token = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().ReadJsonWebToken(corpo.GetProperty("accessToken").GetString());
        Assert.Equal("1", token.GetClaim("trocar_senha").Value);
    }

    [Fact]
    public async Task Com_troca_pendente_endpoints_de_dados_retornam_403()
    {
        var (email, _) = await CriarUsuarioAsync(deveTrocar: true);
        var cliente = await ApiFactory.ClienteComo(fabrica, email);

        var requisicoes = new (HttpMethod Metodo, string Caminho)[]
        {
            (HttpMethod.Get, "/api/cursos"),
            (HttpMethod.Get, "/api/usuarios/profissionais"),
            (HttpMethod.Post, "/api/cursos"),
            (HttpMethod.Get, "/api/auth/senha"), // só o POST é liberado
            (HttpMethod.Post, "/api/auth/senha/"), // a liberação é pelo caminho exato
        };
        foreach (var (metodo, caminho) in requisicoes)
        {
            var resposta = await cliente.SendAsync(new HttpRequestMessage(metodo, caminho)
            {
                Content = JsonContent.Create(new { senhaAtual = ApiFactory.SenhaPadrao, novaSenha = NovaSenha, nome = "Curso Bloqueado" }),
            });
            Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
            var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Defina uma nova senha para continuar.", corpo.GetProperty("title").GetString());
        }
        // Rotas anônimas seguem livres.
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task Com_troca_pendente_eu_logout_e_senha_funcionam()
    {
        var (email, _) = await CriarUsuarioAsync(deveTrocar: true);
        var cliente = await ApiFactory.ClienteComo(fabrica, email);

        var eu = await cliente.GetFromJsonAsync<JsonElement>("/api/auth/eu");
        Assert.True(eu.GetProperty("deveTrocarSenha").GetBoolean());

        // O refresh reemite o claim enquanto a troca estiver pendente.
        var refresh = await cliente.SendAsync(ComCabecalhoSpa("/api/auth/refresh"));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var renovada = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(renovada.GetProperty("usuario").GetProperty("deveTrocarSenha").GetBoolean());
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", renovada.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/cursos")).StatusCode);

        // A troca chega ao handler (400 de validação, não o 403 do bloqueio).
        Assert.NotEmpty(await ErrosAsync(await TrocarAsync(cliente, "errada", NovaSenha), "senhaAtual"));

        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(ComCabecalhoSpa("/api/auth/logout"))).StatusCode);
    }

    [Fact]
    public async Task Senha_atual_errada_retorna_400_no_campo()
    {
        var (email, _) = await CriarUsuarioAsync(deveTrocar: true);
        var cliente = await ApiFactory.ClienteComo(fabrica, email);

        Assert.Equal(["Senha incorreta."], await ErrosAsync(await TrocarAsync(cliente, "SenhaErrada123", NovaSenha), "senhaAtual"));
    }

    [Fact]
    public async Task Tentativas_erradas_bloqueiam_a_troca_e_o_login()
    {
        var (email, _) = await CriarUsuarioAsync(deveTrocar: true);
        var cliente = await ApiFactory.ClienteComo(fabrica, email);

        for (var i = 0; i < 5; i++) // Lockout.MaxFailedAccessAttempts
            Assert.Equal(["Senha incorreta."], await ErrosAsync(await TrocarAsync(cliente, "SenhaErrada123", NovaSenha), "senhaAtual"));

        Assert.Equal(["Muitas tentativas. Tente novamente mais tarde."],
            await ErrosAsync(await TrocarAsync(cliente, ApiFactory.SenhaPadrao, NovaSenha), "senhaAtual"));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await ApiFactory.NovoCliente(fabrica).PostAsJsonAsync("/api/auth/login", new { email, senha = ApiFactory.SenhaPadrao })).StatusCode);
    }

    [Fact]
    public async Task Senha_fraca_retorna_400_em_portugues()
    {
        var (email, _) = await CriarUsuarioAsync(deveTrocar: true);
        var cliente = await ApiFactory.ClienteComo(fabrica, email);

        var erros = await ErrosAsync(await TrocarAsync(cliente, ApiFactory.SenhaPadrao, "curta"), "novaSenha");

        Assert.Contains("A senha deve ter pelo menos 10 caracteres.", erros);
        Assert.Contains("A senha deve ter pelo menos um número.", erros);
        Assert.Contains("A senha deve ter pelo menos uma letra maiúscula.", erros);
    }

    [Fact]
    public async Task Nova_igual_a_atual_retorna_400()
    {
        var (email, _) = await CriarUsuarioAsync(deveTrocar: true);
        var cliente = await ApiFactory.ClienteComo(fabrica, email);

        Assert.Equal(["A nova senha deve ser diferente da atual."],
            await ErrosAsync(await TrocarAsync(cliente, ApiFactory.SenhaPadrao, ApiFactory.SenhaPadrao), "novaSenha"));
    }

    [Fact]
    public async Task Troca_libera_o_acesso_e_derruba_outras_sessoes()
    {
        var (email, id) = await CriarUsuarioAsync(deveTrocar: true);
        var cliente = await ApiFactory.ClienteComo(fabrica, email);
        var outra = await ApiFactory.ClienteComo(fabrica, email);
        Assert.Equal(HttpStatusCode.OK, (await outra.GetAsync("/api/auth/eu")).StatusCode); // preenche o cache do carimbo

        var resposta = await TrocarAsync(cliente, ApiFactory.SenhaPadrao, NovaSenha);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains(resposta.Headers.GetValues("Set-Cookie"), c => c.StartsWith("nap_refresh="));
        var sessao = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(sessao.GetProperty("usuario").GetProperty("deveTrocarSenha").GetBoolean());
        var tokenAntigo = cliente.DefaultRequestHeaders.Authorization;
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/cursos")).StatusCode);
        // O novo cookie de refresh vale.
        Assert.Equal(HttpStatusCode.OK, (await cliente.SendAsync(ComCabecalhoSpa("/api/auth/refresh"))).StatusCode);

        // A outra sessão e o token anterior caíram.
        Assert.Equal(HttpStatusCode.Unauthorized, (await outra.GetAsync("/api/auth/eu")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await outra.SendAsync(ComCabecalhoSpa("/api/auth/refresh"))).StatusCode);
        var antigo = ApiFactory.NovoCliente(fabrica);
        antigo.DefaultRequestHeaders.Authorization = tokenAntigo;
        Assert.Equal(HttpStatusCode.Unauthorized, (await antigo.GetAsync("/api/auth/eu")).StatusCode);

        // A senha antiga não entra mais; a nova sim.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await ApiFactory.NovoCliente(fabrica).PostAsJsonAsync("/api/auth/login", new { email, senha = ApiFactory.SenhaPadrao })).StatusCode);
        await ApiFactory.ClienteComo(fabrica, email, NovaSenha);

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<NapDbContext>();
        Assert.True(await db.EventosAuditoria.AnyAsync(e => e.RecursoId == id && e.Acao == "usuario.senha.trocar" && e.UsuarioId == id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Troca_voluntaria_sem_pendencia_funciona()
    {
        var (email, _) = await CriarUsuarioAsync(deveTrocar: false);
        var cliente = await ApiFactory.ClienteComo(fabrica, email);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/cursos")).StatusCode);

        var resposta = await TrocarAsync(cliente, ApiFactory.SenhaPadrao, NovaSenha);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var sessao = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(sessao.GetProperty("usuario").GetProperty("deveTrocarSenha").GetBoolean());
        await ApiFactory.ClienteComo(fabrica, email, NovaSenha);
    }
}
