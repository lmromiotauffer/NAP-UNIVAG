using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nap.Api.Tests;

/// <summary>O processador da outbox roda em segundo plano na Api de teste (intervalo padrão de 5 s).</summary>
public class NotificacoesTests(ApiFactory fabrica)
{
    private static string Hoje => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd");

    private static async Task<Guid> CriarEncaminhamentoAsync(HttpClient equipe, string destino = "Sip")
    {
        var (alunoId, _) = await ApiFactory.CriarAlunoAsync(equipe, await ApiFactory.IdDoUsuarioAsync(equipe));
        var resposta = await equipe.PostAsJsonAsync("/api/encaminhamentos", new
        {
            alunoId, especialidade = "Psicologia", destino, data = Hoje, motivoRestrito = "Motivo clínico sigiloso",
            resumoCompartilhavel = "Resumo pedagógico",
        });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement[]> ListarAsync(HttpClient c) =>
        [.. (await c.GetFromJsonAsync<JsonElement>("/api/notificacoes?tamanho=100")).GetProperty("itens").EnumerateArray()];

    /// <summary>Espera (até 15 s) o processador gerar a notificação do encaminhamento para o usuário do cliente.</summary>
    private static async Task<JsonElement> AguardarAsync(HttpClient c, Guid encaminhamentoId)
    {
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            var achada = (await ListarAsync(c)).FirstOrDefault(n => n.GetProperty("link").GetString() == $"/encaminhamentos/{encaminhamentoId}");
            if (achada.ValueKind != JsonValueKind.Undefined) return achada;
            Assert.True(DateTime.UtcNow < limite, "A notificação não foi gerada em 15 s.");
            await Task.Delay(250);
        }
    }

    [Fact]
    public async Task Criar_encaminhamento_gera_notificacao_para_o_sip()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var id = await CriarEncaminhamentoAsync(ana);
        var sip = await fabrica.ClienteComo("sip@nap.demo");

        var n = await AguardarAsync(sip, id);

        Assert.Equal("Novo encaminhamento: Aluno de Teste", n.GetProperty("titulo").GetString());
        Assert.Equal("Psicologia · SIP", n.GetProperty("texto").GetString());
        Assert.False(n.GetProperty("lida").GetBoolean());
        Assert.DoesNotContain("sigiloso", n.GetRawText());
        Assert.True((await sip.GetFromJsonAsync<JsonElement>("/api/notificacoes/contagem")).GetProperty("naoLidas").GetInt32() >= 1);
    }

    [Fact]
    public async Task Usuario_so_lista_as_proprias_notificacoes()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var id = await CriarEncaminhamentoAsync(ana);
        var sip = await fabrica.ClienteComo("sip@nap.demo");
        await AguardarAsync(sip, id);

        Assert.DoesNotContain(await ListarAsync(ana), n => n.GetProperty("link").GetString() == $"/encaminhamentos/{id}");
    }

    [Fact]
    public async Task Marcar_lida_de_outro_usuario_retorna_404()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var id = await CriarEncaminhamentoAsync(ana);
        var sip = await fabrica.ClienteComo("sip@nap.demo");
        var notificacaoId = (await AguardarAsync(sip, id)).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await ana.PostAsync($"/api/notificacoes/{notificacaoId}/lida", null)).StatusCode);
        Assert.False((await AguardarAsync(sip, id)).GetProperty("lida").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await sip.PostAsync($"/api/notificacoes/{notificacaoId}/lida", null)).StatusCode);
        Assert.True((await AguardarAsync(sip, id)).GetProperty("lida").GetBoolean());
    }

    [Fact]
    public async Task Retorno_notifica_o_solicitante_e_marcar_todas_zera_a_contagem()
    {
        var (email, _) = await ApiFactory.CriarUsuarioAsync(fabrica);
        var solicitante = await ApiFactory.ClienteComo(fabrica, email);
        var id = await CriarEncaminhamentoAsync(solicitante);
        var sip = await fabrica.ClienteComo("sip@nap.demo");
        var versao = (await sip.GetFromJsonAsync<JsonElement>($"/api/encaminhamentos/{id}")).GetProperty("versao").GetString();
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/encaminhamentos/{id}/retornos")
        {
            Content = JsonContent.Create(new { acao = "Acolhimento", data = Hoje }),
        };
        req.Headers.Add("If-Match", $"\"{versao}\"");
        Assert.Equal(HttpStatusCode.NoContent, (await sip.SendAsync(req)).StatusCode);

        var n = await AguardarAsync(solicitante, id);
        Assert.Equal("Retorno do encaminhamento de Aluno de Teste", n.GetProperty("titulo").GetString());
        Assert.Equal("Acolhimento: Acolhido", n.GetProperty("texto").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await solicitante.PostAsync("/api/notificacoes/lidas", null)).StatusCode);
        Assert.Equal(0, (await solicitante.GetFromJsonAsync<JsonElement>("/api/notificacoes/contagem")).GetProperty("naoLidas").GetInt32());
        Assert.Empty((await solicitante.GetFromJsonAsync<JsonElement>("/api/notificacoes?naoLidas=true")).GetProperty("itens").EnumerateArray());
    }
}
