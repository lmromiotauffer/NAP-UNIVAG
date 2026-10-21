using System.Net;

namespace Nap.Api.Tests;

public class HealthTests(ApiFactory fabrica)
{
    [Fact]
    public async Task Health_live_ok_e_ready_degradado_sem_redis()
    {
        var cliente = ApiFactory.NovoCliente(fabrica);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health/live")).StatusCode);
        var pronto = await cliente.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, pronto.StatusCode);
        Assert.Equal("Healthy", await pronto.Content.ReadAsStringAsync());

        await using var semRedis = ApiFactory.SemRedis(fabrica);
        var clienteSemRedis = ApiFactory.NovoCliente(semRedis);

        Assert.Equal(HttpStatusCode.OK, (await clienteSemRedis.GetAsync("/health/live")).StatusCode);
        var degradado = await clienteSemRedis.GetAsync("/health/ready"); // Degraded mapeia para 200
        Assert.Equal(HttpStatusCode.OK, degradado.StatusCode);
        Assert.Equal("Degraded", await degradado.Content.ReadAsStringAsync());
    }
}
