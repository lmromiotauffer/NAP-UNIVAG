using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nap.Api.Infra;

namespace Nap.Api.Tests;

/// <summary>Só existe no assembly de testes; registrado via <c>AddApplicationPart</c> para forçar uma exceção não prevista.</summary>
[Route("api/teste")]
[Authorize]
public class TesteController : ControllerBase
{
    [HttpGet("erro")]
    public IActionResult Erro() => throw new InvalidOperationException("detalhe interno");
}

public class ErrosTests(ApiFactory fabrica)
{
    [Fact]
    public async Task Validacao_retorna_400_com_errors_por_campo()
    {
        var cliente = await fabrica.ClienteComo("coordenacao@nap.demo");

        var resposta = await cliente.PostAsJsonAsync("/api/alunos", new { });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(corpo.GetProperty("errors").TryGetProperty("nomeCompleto", out var erros));
        Assert.NotEmpty(erros.EnumerateArray());
    }

    [Fact]
    public async Task Conflito_de_agenda_retorna_409()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var (alunoId, _) = await ApiFactory.CriarAlunoAsync(cliente, await ApiFactory.IdDoUsuarioAsync(cliente));
        // semana aleatória (testes em paralelo compartilham a agenda da Ana), sempre numa quarta-feira: fim de semana é rejeitado
        var inicio = new DateTimeOffset(2031, 1, 1, 14, 0, 0, TimeSpan.Zero).AddDays(7 * Random.Shared.Next(0, 3000));
        var corpo = new { tipo = "Atendimento", titulo = "Atendimento", inicio, fim = inicio.AddHours(1), alunoId };

        Assert.Equal(HttpStatusCode.Created, (await cliente.PostAsJsonAsync("/api/agenda", corpo)).StatusCode);
        var segundo = await cliente.PostAsJsonAsync("/api/agenda", corpo);

        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
    }

    [Fact]
    public async Task Bloqueio_sobre_compromisso_existente_retorna_409_com_conflitos_e_atendimento_sem_lista()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var (alunoId, _) = await ApiFactory.CriarAlunoAsync(cliente, await ApiFactory.IdDoUsuarioAsync(cliente));
        var inicio = new DateTimeOffset(2031, 1, 1, 14, 0, 0, TimeSpan.Zero).AddDays(7 * Random.Shared.Next(0, 3000));
        var atendimento = await cliente.PostAsJsonAsync("/api/agenda", new { tipo = "Atendimento", titulo = "Atendimento", inicio, fim = inicio.AddHours(1), alunoId });
        Assert.Equal(HttpStatusCode.Created, atendimento.StatusCode);
        var criado = await atendimento.Content.ReadFromJsonAsync<JsonElement>();

        var bloqueio = await cliente.PostAsJsonAsync("/api/agenda", new { tipo = "Bloqueio", titulo = "Bloqueio", inicio, fim = inicio.AddHours(1) });
        var outroAtendimento = await cliente.PostAsJsonAsync("/api/agenda", new { tipo = "Atendimento", titulo = "Atendimento", inicio, fim = inicio.AddHours(1), alunoId });

        Assert.Equal(HttpStatusCode.Conflict, bloqueio.StatusCode);
        var corpo = await bloqueio.Content.ReadFromJsonAsync<JsonElement>();
        var conflito = Assert.Single(corpo.GetProperty("conflitos").EnumerateArray());
        Assert.Equal(criado.GetProperty("id").GetGuid(), conflito.GetProperty("id").GetGuid());
        Assert.Equal("Aluno de Teste", conflito.GetProperty("aluno").GetString());
        Assert.Equal(HttpStatusCode.Conflict, outroAtendimento.StatusCode);
        Assert.False((await outroAtendimento.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("conflitos", out _));
    }

    // Guarda a paridade dos predicados de ListarConflitosAsync com ExisteConflitoAsync: atividade avulsa ocupa o horário do bloqueio.
    [Fact]
    public async Task Bloqueio_sobre_atividade_avulsa_lista_a_atividade()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        // dia útil passado e aleatório (11h-14h UTC = 07h-10h locais), sem colidir com outros testes
        var inicio = new DateTimeOffset(2012, 1, 2, 11, 0, 0, TimeSpan.Zero).AddDays(7 * Random.Shared.Next(0, 500)).AddHours(Random.Shared.Next(0, 3));
        var atividade = await cliente.PostAsJsonAsync("/api/atividades", new
        {
            inicio, fim = inicio.AddHours(1), tipo = "RodaConversa", descricao = "Roda", justificativaAvulso = "Atividade não agendada",
        });
        Assert.Equal(HttpStatusCode.Created, atividade.StatusCode);
        var criada = await atividade.Content.ReadFromJsonAsync<JsonElement>();

        var bloqueio = await cliente.PostAsJsonAsync("/api/agenda", new { tipo = "Bloqueio", titulo = "Bloqueio", inicio, fim = inicio.AddHours(1) });

        Assert.Equal(HttpStatusCode.Conflict, bloqueio.StatusCode);
        var conflito = Assert.Single((await bloqueio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("conflitos").EnumerateArray());
        Assert.Equal(criada.GetProperty("id").GetGuid(), conflito.GetProperty("id").GetGuid());
        Assert.Equal("Atividade", conflito.GetProperty("tipo").GetString());
    }

    // O validador não olha a demanda; só o domínio (Atendimento.ValidarCoerencia) rejeita Realizado sem demanda principal.
    [Fact]
    public async Task Regra_de_dominio_retorna_422()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var (alunoId, _) = await ApiFactory.CriarAlunoAsync(cliente, await ApiFactory.IdDoUsuarioAsync(cliente));
        var fim = new DateTimeOffset(2026, 9, 1, 15, 0, 0, TimeSpan.Zero); // terça-feira passada, fixa

        var resposta = await cliente.PostAsJsonAsync("/api/atendimentos", new
        {
            alunoId, inicio = fim.AddHours(-1), fim, tipo = "Acompanhamento", estado = "Realizado",
            justificativaAvulso = "Atendimento avulso de teste",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Erro_inesperado_retorna_500_generico_sem_stack()
    {
        await using var comTeste = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddControllers().AddApplicationPart(typeof(TesteController).Assembly)));
        var cliente = await ApiFactory.ClienteComo(comTeste, "coordenacao@nap.demo");

        var resposta = await cliente.GetAsync("/api/teste/erro");

        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = JsonDocument.Parse(texto).RootElement;
        Assert.Equal("Ocorreu um erro inesperado", corpo.GetProperty("title").GetString());
        Assert.True(corpo.TryGetProperty("traceId", out _));
        Assert.DoesNotContain("detalhe interno", texto);
        Assert.DoesNotContain("   at ", texto);
        Assert.DoesNotContain("Nap.", texto);
        Assert.DoesNotContain("InvalidOperationException", texto);
    }

    [Theory]
    [InlineData(typeof(OperationCanceledException))]
    [InlineData(typeof(IOException))]
    public async Task Cancelamento_por_desconexao_do_cliente_vira_499_sem_corpo_nem_log_de_erro(Type tipo)
    {
        var logs = new LoggerCapturado();
        var http = new DefaultHttpContext { RequestAborted = new CancellationToken(true) };
        http.Response.Body = new MemoryStream();

        var tratado = await new ManipuladorExcecoes(logs.CreateLogger<ManipuladorExcecoes>())
            .TryHandleAsync(http, (Exception)Activator.CreateInstance(tipo)!, CancellationToken.None);

        Assert.True(tratado);
        Assert.Equal(499, http.Response.StatusCode);
        Assert.Equal(0, http.Response.Body.Length);
        Assert.Empty(logs.Entradas);
    }

    [Fact]
    public async Task Cancelamento_sem_desconexao_continua_500()
    {
        var logs = new LoggerCapturado();
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var http = new DefaultHttpContext { RequestServices = escopo.ServiceProvider };
        http.Response.Body = new MemoryStream();

        await new ManipuladorExcecoes(logs.CreateLogger<ManipuladorExcecoes>())
            .TryHandleAsync(http, new OperationCanceledException(), CancellationToken.None);

        Assert.Equal(500, http.Response.StatusCode);
        Assert.Equal(LogLevel.Error, Assert.Single(logs.Entradas).Nivel);
    }
}

/// <summary>Logger em memória (sem biblioteca de mock): guarda nível e mensagem formatada.</summary>
public sealed class LoggerCapturado : ILoggerProvider, ILogger
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<(LogLevel Nivel, string Mensagem)> _entradas = new();
    public IReadOnlyList<(LogLevel Nivel, string Mensagem)> Entradas => [.. _entradas];

    public ILogger CreateLogger(string categoryName) => this;
    public ILogger<T> CreateLogger<T>() => new Logger<T>(new LoggerFactory([this]));
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _entradas.Enqueue((logLevel, formatter(state, exception)));
    public void Dispose() { }
}
