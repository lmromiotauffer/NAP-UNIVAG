using Microsoft.EntityFrameworkCore;
using Nap.Domain.Producao;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class ProducaoPersistenciaTests(SqlServerFixture fx)
{
    private static readonly DateOnly Segunda = new(2026, 9, 14);
    private static readonly DateTimeOffset Agora = new(2026, 9, 30, 12, 0, 0, TimeSpan.FromHours(-4));

    [Fact]
    public async Task Round_trip_fechamento_com_eventos()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var coord = await fx.CriarProfissionalAsync();
        var f = FechamentoSemanal.Criar(prof, Segunda);
        f.Fechar(prof, Agora, new DateOnly(2026, 9, 30));
        f.Reabrir("Esqueci um atendimento", coord, Agora.AddHours(1));
        await using (var db = fx.CriarContexto())
        {
            new FechamentoRepository(db).Adicionar(f);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var lido = await new FechamentoRepository(leitura).ObterAsync(prof, Segunda, ct);
        Assert.NotNull(lido);
        Assert.False(lido.Fechada);
        Assert.Equal([TipoEventoFechamento.Fechamento, TipoEventoFechamento.Reabertura], lido.Eventos.OrderBy(e => e.Em).Select(e => e.Tipo));
        var reabertura = lido.Eventos.MaxBy(e => e.Em)!;
        Assert.Equal((coord, "Esqueci um atendimento", Agora.AddHours(1)), (reabertura.AutorId, reabertura.Justificativa, reabertura.Em));
        Assert.Null(await new FechamentoRepository(leitura).ObterAsync(prof, Segunda.AddDays(7), ct));
    }

    [Fact]
    public async Task Fechamento_duplicado_viola_indice()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        await using (var db = fx.CriarContexto())
        {
            db.Add(FechamentoSemanal.Criar(prof, Segunda));
            await db.SaveChangesAsync(ct);
        }

        await using var outro = fx.CriarContexto();
        outro.Add(FechamentoSemanal.Criar(prof, Segunda));
        await Assert.ThrowsAsync<DbUpdateException>(() => outro.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Round_trip_ajuste()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var coord = await fx.CriarProfissionalAsync();
        var ajuste = AjusteProducao.Criar(prof, new DateOnly(2026, 9, 15), CategoriaProducao.ReunioesEquipe, 2, -30,
            "Reunião não registrada", coord, Agora);
        await using (var db = fx.CriarContexto())
        {
            new AjusteProducaoRepository(db).Adicionar(ajuste);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var lido = await leitura.AjustesProducao.SingleAsync(a => a.Id == ajuste.Id, ct);
        Assert.Equal((prof, new DateOnly(2026, 9, 15), CategoriaProducao.ReunioesEquipe, 2, -30, "Reunião não registrada", coord, Agora),
            (lido.ProfissionalId, lido.Data, lido.Categoria, lido.Ocorrencias, lido.Minutos, lido.Justificativa, lido.AutorId, lido.Em));
    }
}
