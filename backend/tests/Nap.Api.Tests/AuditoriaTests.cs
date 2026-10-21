using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nap.Infrastructure.Persistencia;

namespace Nap.Api.Tests;

public class AuditoriaTests(ApiFactory fabrica)
{
    private async Task<List<EventoAuditoria>> EventosAsync(Guid recursoId)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<NapDbContext>().EventosAuditoria.AsNoTracking()
            .Where(e => e.RecursoId == recursoId).ToListAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Politica_negada_grava_acesso_negado_com_rota_e_id()
    {
        var gestao = await fabrica.ClienteComo("gestao@nap.demo");
        var gestaoId = await ApiFactory.IdDoUsuarioAsync(gestao);
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await gestao.GetAsync($"/api/alunos/{id}")).StatusCode);

        var evento = Assert.Single(await EventosAsync(id));
        Assert.Equal(("acesso.negado", "GET api/alunos/{id}", "negado", (Guid?)gestaoId),
            (evento.Acao, evento.Recurso, evento.Resultado, evento.UsuarioId));
    }

    [Fact]
    public async Task Forbidden_do_handler_grava_acesso_negado_com_rota_e_id()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var anaId = await ApiFactory.IdDoUsuarioAsync(ana);
        // Destinatário próprio: não mexe na contagem de não lidas que outros testes observam.
        var (_, destinatarioId) = await ApiFactory.CriarUsuarioAsync(fabrica);
        var (alunoId, _) = await ApiFactory.CriarAlunoAsync(ana, anaId);
        var envio = await ana.PostAsJsonAsync("/api/comunicacoes", new
        {
            destinatarioId, tipo = "Devolutiva", assunto = "Devolutiva", corpo = "Segue a devolutiva.", alunoId,
        });
        Assert.Equal(HttpStatusCode.Created, envio.StatusCode);
        var id = (await envio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Quem enviou vê a mensagem, mas só o destinatário a marca como lida: 403 do handler.
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.PostAsync($"/api/comunicacoes/{id}/lida", null)).StatusCode);

        var evento = Assert.Single(await EventosAsync(id), e => e.Resultado == "negado");
        Assert.Equal(("acesso.negado", "POST api/comunicacoes/{id}/lida", (Guid?)anaId), (evento.Acao, evento.Recurso, evento.UsuarioId));
    }

    [Fact]
    public async Task Nao_registra_404_nem_401()
    {
        var anonimo = Guid.NewGuid();
        var inexistente = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await ApiFactory.NovoCliente(fabrica).GetAsync($"/api/alunos/{anonimo}")).StatusCode);
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        Assert.Equal(HttpStatusCode.NotFound, (await coordenacao.GetAsync($"/api/alunos/{inexistente}")).StatusCode);

        Assert.Empty(await EventosAsync(anonimo));
        Assert.Empty(await EventosAsync(inexistente));
    }

    [Fact]
    public async Task Profissional_recebe_403_e_coordenacao_consulta_a_trilha()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var anaId = await ApiFactory.IdDoUsuarioAsync(ana);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.GetAsync("/api/auditoria")).StatusCode);

        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var resposta = await coordenacao.GetAsync($"/api/auditoria?inicio={hoje.AddDays(-1):yyyy-MM-dd}&fim={hoje.AddDays(1):yyyy-MM-dd}&usuarioId={anaId}&acao=acesso.&recurso=GET%20api/auditoria&resultado=negado&tamanho=100");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var pagina = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        var item = pagina.GetProperty("itens").EnumerateArray().First();
        Assert.Equal("Ana Beatriz Rocha", item.GetProperty("usuario").GetString());
        Assert.Equal("acesso.negado", item.GetProperty("acao").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("recursoId").ValueKind);

        Assert.Equal(HttpStatusCode.BadRequest, (await coordenacao.GetAsync("/api/auditoria?inicio=2025-01-01&fim=2026-06-01")).StatusCode);

        var coordenacaoId = await ApiFactory.IdDoUsuarioAsync(coordenacao);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        Assert.True(await escopo.ServiceProvider.GetRequiredService<NapDbContext>().EventosAuditoria.AnyAsync(e =>
            e.UsuarioId == coordenacaoId && e.Acao == "auditoria.consulta" && e.Recurso == "Auditoria" && e.Resultado == "sucesso",
            TestContext.Current.CancellationToken));
    }
}
