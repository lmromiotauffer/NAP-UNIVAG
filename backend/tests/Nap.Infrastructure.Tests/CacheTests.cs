using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Painel;
using Nap.Application.Features.Painel.Queries;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Infrastructure.Cache;
using StackExchange.Redis;

namespace Nap.Infrastructure.Tests;

public class CacheTests(RedisFixture redis)
{
    private record Filtros(DateOnly Semana, Guid? CursoId);

    [Fact]
    public async Task Redis_indisponivel_no_cache_de_consultas_usa_fonte()
    {
        var ct = TestContext.Current.CancellationToken;
        // Endpoint sem servidor: cada comando falha em ~0,5 s (sem esperar o backlog padrão de 5 s).
        await using var morto = await ConnectionMultiplexer.ConnectAsync(
            "127.0.0.1:1,abortConnect=false,connectTimeout=500,asyncTimeout=500,syncTimeout=500,connectRetry=1");
        var cache = new CacheConsultasRedis(morto, new RelogioTeste(DateTimeOffset.UtcNow), NullLogger<CacheConsultasRedis>.Instance);
        var chamadas = 0;

        for (var i = 0; i < 2; i++)
            Assert.Equal(42, await cache.ObterOuCriarAsync("todos", new Filtros(default, null), _ => { chamadas++; return Task.FromResult(42); }, ct));
        await cache.InvalidarAreaAsync(ct); // nunca lança

        Assert.Equal(2, chamadas);
        // Falha da própria fonte não é engolida como se fosse falha do Redis.
        await Assert.ThrowsAsync<ArgumentException>(() => cache.ObterOuCriarAsync<int>("todos", 1, _ => throw new ArgumentException("fonte"), ct));
    }

    [Fact]
    public async Task Cache_reaproveita_e_muda_de_chave_por_geracao_dia_escopo_e_filtros()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexao = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var relogio = new RelogioTeste(new DateTimeOffset(2026, 10, 2, 23, 50, 0, TimeSpan.FromHours(-4)));
        var cache = new CacheConsultasRedis(conexao, relogio, NullLogger<CacheConsultasRedis>.Instance);
        var filtros = new Filtros(new DateOnly(2026, 9, 28), null);
        var chamadas = 0;
        Task<int> Obter(string escopo, Filtros f) => cache.ObterOuCriarAsync(escopo, f, _ => Task.FromResult(++chamadas), ct);

        Assert.Equal(1, await Obter("todos", filtros));
        Assert.Equal(1, await Obter("todos", filtros)); // acerto
        Assert.Equal(2, await Obter("outro", filtros));
        Assert.Equal(3, await Obter("todos", filtros with { CursoId = Guid.NewGuid() }));

        await cache.InvalidarAreaAsync(ct);
        Assert.Equal(4, await Obter("todos", filtros));
        Assert.Equal(4, await Obter("todos", filtros));

        relogio.Agora = relogio.Agora.AddMinutes(20); // virou o dia local: "AgendaHoje"/"Parcial" não podem vir de ontem
        Assert.Equal(5, await Obter("todos", filtros));
    }

    [Fact]
    public async Task Painel_real_volta_do_cache_igual_ao_gravado()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexao = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var cache = new CacheConsultasRedis(conexao, new RelogioTeste(DateTimeOffset.UtcNow), NullLogger<CacheConsultasRedis>.Instance);
        var filtros = new FiltrosPainel(new DateOnly(2026, 9, 28), null, Guid.NewGuid());
        var inicio = new DateTimeOffset(2026, 10, 2, 13, 0, 0, TimeSpan.Zero);
        var painel = new PainelDto(new PeriodoDto(new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 2), true),
            new ProducaoSemanaDto(3, 150, 3, 1, 0.25m, null, 1),
            [new CompromissoDto(Guid.NewGuid(), TipoCompromisso.Atendimento, "Atendimento", inicio, inicio.AddMinutes(50), Guid.NewGuid(),
                "Profissional", Guid.NewGuid(), "Aluno", EstadoCompromisso.Concluido, EstadoAtendimento.Ausente, null, "Descrição", "AAAAAAAAB9E=", null, null, false)],
            [new SerieMensalDto(2026, 9, 3, 150, false)], [new ContagemDto("x", "Curso", 2)], null,
            [new AlertaDto("faltas_recorrentes", "Faltas recorrentes", "Aluno: 3 faltas consecutivas", Guid.NewGuid(), 3)], null, null);

        await cache.ObterOuCriarAsync("todos", filtros, _ => Task.FromResult(painel), ct);
        // Se o DTO não desserializasse, o cache cairia em silêncio na fonte, que aqui lança.
        var doCache = await cache.ObterOuCriarAsync<PainelDto>("todos", filtros, _ => throw new InvalidOperationException("não veio do cache"), ct);

        Assert.Equal(JsonSerializer.Serialize(painel), JsonSerializer.Serialize(doCache));
    }
}
