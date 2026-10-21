using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nap.Infrastructure.Persistencia;

namespace Nap.Api.Tests;

public class AutorizacaoForaEscalaTests(ApiFactory fabrica)
{
    // Data distante e aleatória: o banco é compartilhado entre os testes.
    private static DateTimeOffset DiaAleatorio() =>
        new DateTimeOffset(2400, 1, 1, 22, 0, 0, TimeSpan.Zero).AddDays(Random.Shared.Next(0, 30000));

    [Fact]
    public async Task Coordenacao_cria_lista_e_revoga_com_auditoria_e_profissional_recebe_403()
    {
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var anaId = await ApiFactory.IdDoUsuarioAsync(ana);
        var inicio = DiaAleatorio();
        var corpo = new { profissionalId = anaId, inicio, fim = inicio.AddHours(3), justificativa = "Plantão do vestibular" };

        Assert.Equal(HttpStatusCode.Forbidden, (await ana.PostAsJsonAsync("/api/escalas/autorizacoes", corpo)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.GetAsync("/api/escalas/autorizacoes")).StatusCode);

        var criada = await coordenacao.PostAsJsonAsync("/api/escalas/autorizacoes", corpo);
        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        var id = (await criada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var dia = DateOnly.FromDateTime(inicio.UtcDateTime);
        var lista = await coordenacao.GetFromJsonAsync<JsonElement>(
            $"/api/escalas/autorizacoes?profissionalId={anaId}&inicio={dia:yyyy-MM-dd}&fim={dia.AddDays(1):yyyy-MM-dd}");
        var item = Assert.Single(lista.GetProperty("itens").EnumerateArray());
        Assert.Equal((id, "Plantão do vestibular", JsonValueKind.Null), (item.GetProperty("id").GetGuid(),
            item.GetProperty("justificativa").GetString(), item.GetProperty("revogadaEm").ValueKind));

        Assert.Equal(HttpStatusCode.Forbidden, (await ana.PostAsync($"/api/escalas/autorizacoes/{id}/revogar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await coordenacao.PostAsync($"/api/escalas/autorizacoes/{id}/revogar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await coordenacao.PostAsync($"/api/escalas/autorizacoes/{id}/revogar", null)).StatusCode);

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var acoes = await escopo.ServiceProvider.GetRequiredService<NapDbContext>().EventosAuditoria.AsNoTracking()
            .Where(e => e.RecursoId == id && e.Recurso == "AutorizacaoForaEscala" && e.Resultado == "sucesso")
            .Select(e => e.Acao).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["escala.autorizacao.criar", "escala.autorizacao.revogar"], acoes.Order());
    }

    [Fact]
    public async Task Justificativa_curta_retorna_400()
    {
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var inicio = DiaAleatorio();

        var resposta = await coordenacao.PostAsJsonAsync("/api/escalas/autorizacoes",
            new { profissionalId = await ApiFactory.IdDoUsuarioAsync(ana), inicio, fim = inicio.AddHours(2), justificativa = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.True((await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("justificativa", out _));
    }
}
