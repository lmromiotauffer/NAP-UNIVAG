using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nap.Application.Comum;

namespace Nap.Api.Tests;

public class IdempotenciaTests(ApiFactory fabrica)
{
    [Fact]
    public async Task Post_repetido_com_mesma_idempotency_key_retorna_mesmo_id()
    {
        var cliente = await fabrica.ClienteComo("coordenacao@nap.demo");
        var profissionalId = await ApiFactory.IdDoUsuarioAsync(await fabrica.ClienteComo("ana.beatriz@nap.demo"));
        var cursos = await cliente.GetFromJsonAsync<JsonElement>("/api/cursos");
        var matricula = "I" + Guid.NewGuid().ToString("N")[..10];
        var chave = Guid.NewGuid().ToString();
        var corpo = new
        {
            nomeCompleto = "Aluno Idempotente", matricula, cursoId = cursos.EnumerateArray().First().GetProperty("id").GetGuid(),
            semestre = 2, inicioAcompanhamento = "2026-02-01", profissionalResponsavelId = profissionalId,
        };

        async Task<Guid> Postar()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/alunos") { Content = JsonContent.Create(corpo) };
            req.Headers.Add("Idempotency-Key", chave);
            var resposta = await cliente.SendAsync(req);
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
            return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        var primeiro = await Postar();
        var segundo = await Postar();

        Assert.Equal(primeiro, segundo);
        var busca = await cliente.GetFromJsonAsync<JsonElement>($"/api/alunos?busca={matricula}");
        Assert.Equal(1, busca.GetProperty("total").GetInt32());
    }

    private static async Task<object> CorpoAlunoAsync(HttpClient cliente, Guid profissionalId, int semestre = 2)
    {
        var cursos = await cliente.GetFromJsonAsync<JsonElement>("/api/cursos");
        return new
        {
            nomeCompleto = "Aluno Idempotente", matricula = "I" + Guid.NewGuid().ToString("N")[..10],
            cursoId = cursos.EnumerateArray().First().GetProperty("id").GetGuid(), semestre, inicioAcompanhamento = "2026-02-01",
            profissionalResponsavelId = profissionalId,
        };
    }

    private static Task<HttpResponseMessage> PostarAsync(HttpClient cliente, string url, object corpo, string chave)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(corpo) };
        req.Headers.Add("Idempotency-Key", chave);
        return cliente.SendAsync(req);
    }

    [Fact]
    public async Task Falha_da_acao_libera_a_chave_para_nova_tentativa()
    {
        var cliente = await fabrica.ClienteComo("coordenacao@nap.demo");
        var profissionalId = await ApiFactory.IdDoUsuarioAsync(await fabrica.ClienteComo("ana.beatriz@nap.demo"));
        var chave = Guid.NewGuid().ToString();

        var invalido = await PostarAsync(cliente, "/api/alunos", await CorpoAlunoAsync(cliente, profissionalId, semestre: 0), chave);
        var corrigido = await PostarAsync(cliente, "/api/alunos", await CorpoAlunoAsync(cliente, profissionalId), chave);

        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        // Sem a liberação, a chave ficaria "em andamento" e a repetição corrigida receberia 409 por 5 minutos.
        Assert.Equal(HttpStatusCode.Created, corrigido.StatusCode);
    }

    [Fact]
    public async Task Chave_em_andamento_retorna_409_e_nao_cria()
    {
        var cliente = await fabrica.ClienteComo("coordenacao@nap.demo");
        var usuarioId = await ApiFactory.IdDoUsuarioAsync(cliente);
        var profissionalId = await ApiFactory.IdDoUsuarioAsync(await fabrica.ClienteComo("ana.beatriz@nap.demo"));
        var chave = Guid.NewGuid().ToString();
        await using (var escopo = fabrica.Services.CreateAsyncScope())
            await escopo.ServiceProvider.GetRequiredService<IChavesIdempotencia>()
                .ReservarAsync(usuarioId, chave, "POST /api/alunos", TestContext.Current.CancellationToken);

        var resposta = await PostarAsync(cliente, "/api/alunos", await CorpoAlunoAsync(cliente, profissionalId), chave);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Mesma_chave_em_outra_rota_retorna_409()
    {
        var cliente = await fabrica.ClienteComo("coordenacao@nap.demo");
        var profissionalId = await ApiFactory.IdDoUsuarioAsync(await fabrica.ClienteComo("ana.beatriz@nap.demo"));
        var chave = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Created,
            (await PostarAsync(cliente, "/api/alunos", await CorpoAlunoAsync(cliente, profissionalId), chave)).StatusCode);

        var inicio = new DateTimeOffset(2031, 3, 5, 14, 0, 0, TimeSpan.Zero);
        var outra = await PostarAsync(cliente, "/api/agenda",
            new { tipo = "Lembrete", titulo = "Lembrete", inicio, fim = inicio.AddHours(1) }, chave);

        Assert.Equal(HttpStatusCode.Conflict, outra.StatusCode);
    }

    [Fact]
    public async Task Falha_ao_concluir_a_chave_tenta_de_novo_loga_critical_e_mantem_o_201()
    {
        var chaves = new ChavesQueFalhamAoConcluir();
        var logs = new LoggerCapturado();
        await using var comFalha = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddScoped<IChavesIdempotencia>(_ => chaves);
            s.AddSingleton<ILoggerProvider>(logs);
        }));
        var cliente = await ApiFactory.ClienteComo(comFalha, "coordenacao@nap.demo");
        var profissionalId = await ApiFactory.IdDoUsuarioAsync(await ApiFactory.ClienteComo(comFalha, "ana.beatriz@nap.demo"));
        var cursos = await cliente.GetFromJsonAsync<JsonElement>("/api/cursos");
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/alunos")
        {
            Content = JsonContent.Create(new
            {
                nomeCompleto = "Aluno Chave Falha", matricula = "F" + Guid.NewGuid().ToString("N")[..10],
                cursoId = cursos.EnumerateArray().First().GetProperty("id").GetGuid(), semestre = 2,
                inicioAcompanhamento = "2026-02-01", profissionalResponsavelId = profissionalId,
            }),
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var resposta = await cliente.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var id = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(2, chaves.TentativasDeConcluir);
        var critico = Assert.Single(logs.Entradas, e => e.Nivel == LogLevel.Critical);
        Assert.Contains(id.ToString(), critico.Mensagem);
        Assert.Contains("/api/alunos", critico.Mensagem);
        Assert.DoesNotContain("Aluno Chave Falha", critico.Mensagem);
    }

    private sealed class ChavesQueFalhamAoConcluir : IChavesIdempotencia
    {
        private int _tentativas;
        public int TentativasDeConcluir => _tentativas;

        public Task<ReservaIdempotencia> ReservarAsync(Guid usuarioId, string chave, string rota, CancellationToken ct) =>
            Task.FromResult(new ReservaIdempotencia(ResultadoReserva.Reservada, null));

        public Task ConcluirAsync(Guid usuarioId, string chave, Guid recursoId, CancellationToken ct)
        {
            Interlocked.Increment(ref _tentativas);
            throw new InvalidOperationException("banco indisponível");
        }

        public Task LiberarAsync(Guid usuarioId, string chave, CancellationToken ct) => Task.CompletedTask;
    }
}
