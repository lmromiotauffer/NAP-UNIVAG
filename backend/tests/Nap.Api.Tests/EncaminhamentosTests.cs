using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nap.Api.Tests;

public class EncaminhamentosTests(ApiFactory fabrica)
{
    private static string Hoje => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd");

    private static async Task<Guid> CriarAsync(HttpClient equipe, Guid alunoId, string especialidade, string destino)
    {
        var resposta = await equipe.PostAsJsonAsync("/api/encaminhamentos", new
        {
            alunoId, especialidade, destino, data = Hoje, motivoRestrito = "Motivo clínico sigiloso",
            resumoCompartilhavel = "Resumo pedagógico", observacoes = "Observação interna",
        });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> PostarAsync(HttpClient c, string url, object corpo, string? versao)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(corpo) };
        if (versao is not null) req.Headers.Add("If-Match", $"\"{versao}\"");
        return await c.SendAsync(req);
    }

    private static async Task<string> VersaoAsync(HttpClient c, Guid id) =>
        (await c.GetFromJsonAsync<JsonElement>($"/api/encaminhamentos/{id}")).GetProperty("versao").GetString()!;

    private static async Task<HttpResponseMessage> RetornoAsync(HttpClient c, Guid id, string acao) =>
        await PostarAsync(c, $"/api/encaminhamentos/{id}/retornos", new { acao, data = Hoje, texto = "ok" }, await VersaoAsync(c, id));

    private async Task<(HttpClient Ana, Guid AlunoId)> AnaComAlunoAsync(string curso = "Direito")
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var (alunoId, _) = await ApiFactory.CriarAlunoAsync(ana, await ApiFactory.IdDoUsuarioAsync(ana), curso);
        return (ana, alunoId);
    }

    [Fact]
    public async Task Jornada_profissional_cria_e_sip_acolhe()
    {
        var (ana, alunoId) = await AnaComAlunoAsync();
        var id = await CriarAsync(ana, alunoId, "Psicologia", "Sip");

        var sip = await fabrica.ClienteComo("sip@nap.demo");
        var detalhe = await sip.GetFromJsonAsync<JsonElement>($"/api/encaminhamentos/{id}");
        Assert.Equal("Motivo clínico sigiloso", detalhe.GetProperty("motivoRestrito").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await RetornoAsync(sip, id, "Acolhimento")).StatusCode);
        Assert.Equal("Acolhido", (await sip.GetFromJsonAsync<JsonElement>($"/api/encaminhamentos/{id}")).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Gestao_recebe_403_na_lista()
    {
        var gestao = await fabrica.ClienteComo("gestao@nap.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await gestao.GetAsync("/api/encaminhamentos")).StatusCode);
    }

    [Fact]
    public async Task Coordenador_de_outro_curso_recebe_404_no_detalhe()
    {
        var (ana, alunoId) = await AnaComAlunoAsync("Psicologia");
        var id = await CriarAsync(ana, alunoId, "Psicologia", "ClinicaEscola");

        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");
        Assert.Equal(HttpStatusCode.NotFound, (await coord.GetAsync($"/api/encaminhamentos/{id}")).StatusCode);
    }

    [Fact]
    public async Task Coordenador_do_curso_nao_recebe_motivo()
    {
        var (ana, alunoId) = await AnaComAlunoAsync();
        var id = await CriarAsync(ana, alunoId, "Psicologia", "ClinicaEscola");

        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");
        var detalhe = await coord.GetFromJsonAsync<JsonElement>($"/api/encaminhamentos/{id}");
        Assert.Equal(JsonValueKind.Null, detalhe.GetProperty("motivoRestrito").ValueKind);
        Assert.Equal(JsonValueKind.Null, detalhe.GetProperty("observacoes").ValueKind);
        // Destino Clínica Escola: o resumo também fica de fora (só aparece quando o destino é a coordenação).
        Assert.Equal(JsonValueKind.Null, detalhe.GetProperty("resumoCompartilhavel").ValueKind);
    }

    [Fact]
    public async Task Coordenador_do_curso_nao_recebe_especialidade_nem_destino_clinico()
    {
        var (ana, alunoId) = await AnaComAlunoAsync();
        var clinico = await CriarAsync(ana, alunoId, "Psiquiatria", "Sip");
        var doCurso = await CriarAsync(ana, alunoId, "CoordenacaoCurso", "CoordenacaoCurso");

        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");
        var detalhe = await coord.GetFromJsonAsync<JsonElement>($"/api/encaminhamentos/{clinico}");
        var itens = (await coord.GetFromJsonAsync<JsonElement>($"/api/encaminhamentos?alunoId={alunoId}"))
            .GetProperty("itens").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetGuid());

        foreach (var json in new[] { detalhe, itens[clinico] })
        {
            Assert.Equal(JsonValueKind.Null, json.GetProperty("especialidade").ValueKind);
            Assert.Equal(JsonValueKind.Null, json.GetProperty("destino").ValueKind);
        }
        Assert.Equal("CoordenacaoCurso", itens[doCurso].GetProperty("destino").GetString());
    }

    [Fact]
    public async Task Sip_recebe_404_em_encaminhamento_externo()
    {
        var (ana, alunoId) = await AnaComAlunoAsync();
        var id = await CriarAsync(ana, alunoId, "Psiquiatria", "Externo");

        var sip = await fabrica.ClienteComo("sip@nap.demo");
        Assert.Equal(HttpStatusCode.NotFound, (await sip.GetAsync($"/api/encaminhamentos/{id}")).StatusCode);
    }

    [Fact]
    public async Task Retorno_sem_if_match_retorna_428()
    {
        var (ana, alunoId) = await AnaComAlunoAsync();
        var id = await CriarAsync(ana, alunoId, "Psicologia", "Externo");

        var resposta = await PostarAsync(ana, $"/api/encaminhamentos/{id}/retornos", new { acao = "Acolhimento", data = Hoje }, null);
        Assert.Equal((HttpStatusCode)428, resposta.StatusCode);
    }

    [Fact]
    public async Task Retorno_em_encaminhamento_com_alta_retorna_422()
    {
        var (ana, alunoId) = await AnaComAlunoAsync();
        var id = await CriarAsync(ana, alunoId, "Psicologia", "Externo");
        foreach (var acao in new[] { "Acolhimento", "InicioAtendimento", "Alta" })
            Assert.Equal(HttpStatusCode.NoContent, (await RetornoAsync(ana, id, acao)).StatusCode);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await RetornoAsync(ana, id, "Evolucao")).StatusCode);
    }

    [Fact]
    public async Task Criacao_repetida_com_mesma_chave_nao_duplica()
    {
        var (ana, alunoId) = await AnaComAlunoAsync();
        var chave = Guid.NewGuid().ToString();
        async Task<HttpResponseMessage> Enviar()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/encaminhamentos")
            {
                Content = JsonContent.Create(new
                {
                    alunoId, especialidade = "Psicologia", destino = "Externo", data = Hoje, motivoRestrito = "Motivo clínico sigiloso",
                    resumoCompartilhavel = "Resumo pedagógico", observacoes = (string?)null,
                }),
            };
            req.Headers.Add("Idempotency-Key", chave);
            return await ana.SendAsync(req);
        }

        Assert.Equal(HttpStatusCode.Created, (await Enviar()).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Enviar()).StatusCode);
        var lista = await ana.GetFromJsonAsync<JsonElement>($"/api/encaminhamentos?alunoId={alunoId}");
        Assert.Equal(1, lista.GetProperty("total").GetInt32());
    }
}
