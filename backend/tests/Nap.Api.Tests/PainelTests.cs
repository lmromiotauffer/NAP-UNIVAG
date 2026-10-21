using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Nap.Infrastructure.Identidade;

namespace Nap.Api.Tests;

public class PainelTests(ApiFactory fabrica)
{
    [Fact]
    public async Task Painel_responde_com_redis_indisponivel()
    {
        await using var semRedis = ApiFactory.SemRedis(fabrica);
        var cliente = await ApiFactory.ClienteComo(semRedis, "coordenacao@nap.demo"); // login e validação de sessão caem no banco

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/painel")).StatusCode);
    }

    [Fact]
    public async Task Sem_redis_painel_sem_token_retorna_401()
    {
        await using var semRedis = ApiFactory.SemRedis(fabrica);

        Assert.Equal(HttpStatusCode.Unauthorized, (await ApiFactory.NovoCliente(semRedis).GetAsync("/api/painel")).StatusCode);
    }

    [Fact]
    public async Task Sem_redis_sessao_encerrada_por_logout_continua_negada()
    {
        await using var semRedis = ApiFactory.SemRedis(fabrica);
        var cliente = await ApiFactory.ClienteComo(semRedis, "coordenacao@nap.demo");
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/painel")).StatusCode);

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout") { Headers = { { "X-Requested-With", "nap-spa" } } };
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(logout)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/painel")).StatusCode);
    }

    [Fact]
    public async Task Sem_redis_token_antigo_apos_mudanca_de_stamp_continua_negado()
    {
        await using var semRedis = ApiFactory.SemRedis(fabrica);
        var (email, id) = await ApiFactory.CriarUsuarioAsync(semRedis);
        var cliente = await ApiFactory.ClienteComo(semRedis, email);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/painel")).StatusCode);

        await using (var escopo = semRedis.Services.CreateAsyncScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentidade>>();
            Assert.True((await usuarios.UpdateSecurityStampAsync((await usuarios.FindByIdAsync(id.ToString()))!)).Succeeded);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/painel")).StatusCode);
    }
}
