using Nap.Application.Comum;
using Nap.Infrastructure.Persistencia;

namespace Nap.Infrastructure.Tests;

public class ChavesIdempotenciaTests(SqlServerFixture fx)
{
    private readonly RelogioTeste _relogio = new(new DateTimeOffset(2026, 10, 2, 15, 0, 0, TimeSpan.Zero));

    private async Task<ResultadoReserva> ReservarAsync(Guid usuario, string chave, CancellationToken ct)
    {
        await using var db = fx.CriarContexto();
        return (await new ChavesIdempotencia(db, _relogio).ReservarAsync(usuario, chave, "POST /api/alunos", ct)).Resultado;
    }

    [Fact]
    public async Task Reserva_abandonada_ha_mais_de_cinco_minutos_e_retomada_por_um_unico_vencedor()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = Guid.NewGuid();
        const string chave = "chave-abandonada";
        Assert.Equal(ResultadoReserva.Reservada, await ReservarAsync(usuario, chave, ct));

        // Antes do prazo, continua em andamento (o processo original pode estar só lento).
        _relogio.Agora = _relogio.Agora.AddMinutes(4);
        Assert.Equal(ResultadoReserva.EmAndamento, await ReservarAsync(usuario, chave, ct));

        // Passado o prazo, várias repetições simultâneas: exatamente uma retoma a reserva.
        _relogio.Agora = _relogio.Agora.AddMinutes(2);
        var resultados = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => ReservarAsync(usuario, chave, ct), ct)));

        Assert.Single(resultados, r => r == ResultadoReserva.Reservada);
        Assert.All(resultados.Where(r => r != ResultadoReserva.Reservada), r => Assert.Equal(ResultadoReserva.EmAndamento, r));
    }
}
