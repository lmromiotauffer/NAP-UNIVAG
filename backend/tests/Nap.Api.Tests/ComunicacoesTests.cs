using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nap.Api.Tests;

public class ComunicacoesTests(ApiFactory fabrica)
{
    private static async Task<Guid> IdAsync(HttpResponseMessage resposta)
    {
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<(HttpClient Ana, HttpClient Coord, Guid CoordId, Guid AlunoId)> CenarioAsync()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");
        var (alunoId, _) = await ApiFactory.CriarAlunoAsync(ana, await ApiFactory.IdDoUsuarioAsync(ana), "Direito");
        return (ana, coord, await ApiFactory.IdDoUsuarioAsync(coord), alunoId);
    }

    private static object Mensagem(Guid destinatarioId, Guid? alunoId, string assunto = "Devolutiva do aluno") => new
    {
        destinatarioId, tipo = "Devolutiva", assunto, corpo = "Segue a devolutiva.", alunoId,
    };

    [Fact]
    public async Task Jornada_profissional_envia_coordenador_responde()
    {
        var (ana, coord, coordId, alunoId) = await CenarioAsync();

        var id = await IdAsync(await ana.PostAsJsonAsync("/api/comunicacoes", Mensagem(coordId, alunoId)));

        var recebida = await coord.GetFromJsonAsync<JsonElement>($"/api/comunicacoes/{id}");
        Assert.True(recebida.GetProperty("podeResponder").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await coord.PostAsync($"/api/comunicacoes/{id}/lida", null)).StatusCode);

        await IdAsync(await coord.PostAsJsonAsync($"/api/comunicacoes/{id}/respostas", new { corpo = "Recebido, obrigado." }));

        var fio = (await ana.GetFromJsonAsync<JsonElement>($"/api/comunicacoes/{id}")).GetProperty("fio");
        Assert.Equal(2, fio.GetArrayLength());
    }

    [Fact]
    public async Task Gestao_recebe_403()
    {
        var gestao = await fabrica.ClienteComo("gestao@nap.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await gestao.GetAsync("/api/comunicacoes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await gestao.GetAsync("/api/comunicacoes/contagem")).StatusCode);
    }

    [Fact]
    public async Task Rascunho_alheio_retorna_404()
    {
        var (ana, coord, _, _) = await CenarioAsync();
        var id = await IdAsync(await ana.PostAsJsonAsync("/api/comunicacoes/rascunhos",
            new { tipo = "Aviso", assunto = "Rascunho", corpo = "Texto" }));

        Assert.Equal(HttpStatusCode.OK, (await ana.GetAsync($"/api/comunicacoes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coord.GetAsync($"/api/comunicacoes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coord.DeleteAsync($"/api/comunicacoes/rascunhos/{id}")).StatusCode);
    }

    [Fact]
    public async Task Rascunho_edita_com_if_match_e_descarta()
    {
        var (ana, _, _, _) = await CenarioAsync();
        var id = await IdAsync(await ana.PostAsJsonAsync("/api/comunicacoes/rascunhos",
            new { tipo = "Aviso", assunto = "Rascunho", corpo = "Texto" }));
        var corpo = new { tipo = "Aviso", assunto = "Rascunho editado", corpo = "Texto novo" };

        Assert.Equal((HttpStatusCode)428, (await ana.PutAsJsonAsync($"/api/comunicacoes/rascunhos/{id}", corpo)).StatusCode);

        var versao = (await ana.GetFromJsonAsync<JsonElement>($"/api/comunicacoes/{id}")).GetProperty("versao").GetString();
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/comunicacoes/rascunhos/{id}") { Content = JsonContent.Create(corpo) };
        req.Headers.Add("If-Match", $"\"{versao}\"");
        Assert.Equal(HttpStatusCode.NoContent, (await ana.SendAsync(req)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync($"/api/comunicacoes/rascunhos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ana.GetAsync($"/api/comunicacoes/{id}")).StatusCode);
    }

    [Fact]
    public async Task Mensagem_de_terceiro_retorna_404()
    {
        var (ana, _, coordId, alunoId) = await CenarioAsync();
        var id = await IdAsync(await ana.PostAsJsonAsync("/api/comunicacoes", Mensagem(coordId, alunoId)));

        var carlos = await fabrica.ClienteComo("carlos.mendes@nap.demo");
        Assert.Equal(HttpStatusCode.NotFound, (await carlos.GetAsync($"/api/comunicacoes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await carlos.PostAsync($"/api/comunicacoes/{id}/lida", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await carlos.PostAsync($"/api/comunicacoes/{id}/arquivar", null)).StatusCode);
    }

    [Fact]
    public async Task Envio_repetido_com_mesma_chave_nao_duplica()
    {
        var (ana, _, coordId, alunoId) = await CenarioAsync();
        var assunto = $"Envio único {Guid.NewGuid():N}";
        var chave = Guid.NewGuid().ToString();

        async Task<Guid> EnviarAsync()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/comunicacoes") { Content = JsonContent.Create(Mensagem(coordId, alunoId, assunto)) };
            req.Headers.Add("Idempotency-Key", chave);
            return await IdAsync(await ana.SendAsync(req));
        }

        Assert.Equal(await EnviarAsync(), await EnviarAsync());
        var lista = await ana.GetFromJsonAsync<JsonElement>($"/api/comunicacoes?caixa=Enviados&busca={assunto}");
        Assert.Equal(1, lista.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Caixa_filtra_por_encaminhamento()
    {
        var (ana, _, coordId, alunoId) = await CenarioAsync();
        var criado = await ana.PostAsJsonAsync("/api/encaminhamentos", new
        {
            alunoId, especialidade = "Psicologia", destino = "ClinicaEscola",
            data = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd"),
            motivoRestrito = "Motivo", resumoCompartilhavel = "Resumo",
        });
        var encaminhamentoId = await IdAsync(criado);
        var vinculada = await IdAsync(await ana.PostAsJsonAsync("/api/comunicacoes", new
        {
            destinatarioId = coordId, tipo = "Aviso", assunto = "Sobre o encaminhamento", corpo = "Texto.", alunoId, encaminhamentoId,
        }));
        await IdAsync(await ana.PostAsJsonAsync("/api/comunicacoes", Mensagem(coordId, alunoId)));

        var lista = await ana.GetFromJsonAsync<JsonElement>($"/api/comunicacoes?caixa=Enviados&encaminhamentoId={encaminhamentoId}");
        Assert.Equal(1, lista.GetProperty("total").GetInt32());
        Assert.Equal(vinculada, lista.GetProperty("itens")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Destinatario_invalido_retorna_400_no_campo()
    {
        var (ana, _, _, alunoId) = await CenarioAsync();
        var gestaoId = await ApiFactory.IdDoUsuarioAsync(await fabrica.ClienteComo("gestao@nap.demo"));

        var resposta = await ana.PostAsJsonAsync("/api/comunicacoes", Mensagem(gestaoId, alunoId));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Contains(erros.EnumerateObject(), e => e.Name.Equals("destinatarioId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Contagem_cai_apos_marcar_lida()
    {
        var (ana, coord, coordId, alunoId) = await CenarioAsync();
        async Task<int> ContarAsync() =>
            (await coord.GetFromJsonAsync<JsonElement>("/api/comunicacoes/contagem")).GetProperty("naoLidas").GetInt32();

        var antes = await ContarAsync();
        var id = await IdAsync(await ana.PostAsJsonAsync("/api/comunicacoes", Mensagem(coordId, alunoId)));
        Assert.Equal(antes + 1, await ContarAsync());

        await coord.PostAsync($"/api/comunicacoes/{id}/lida", null);
        Assert.Equal(antes, await ContarAsync());
    }

    [Fact]
    public async Task Marcar_lida_tambem_marca_a_notificacao_da_mensagem()
    {
        var (ana, coord, coordId, alunoId) = await CenarioAsync();
        var id = await IdAsync(await ana.PostAsJsonAsync("/api/comunicacoes", Mensagem(coordId, alunoId)));
        async Task<JsonElement?> NotificacaoAsync() =>
            (await coord.GetFromJsonAsync<JsonElement>("/api/notificacoes?tamanho=100")).GetProperty("itens").EnumerateArray()
                .Cast<JsonElement?>().FirstOrDefault(n => n!.Value.GetProperty("link").GetString() == $"/comunicacoes/{id}");

        // O processador da outbox gera a notificação em segundo plano (intervalo padrão de 5 s).
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (await NotificacaoAsync() is null)
        {
            Assert.True(DateTime.UtcNow < limite, "A notificação não foi gerada em 15 s.");
            await Task.Delay(250);
        }
        Assert.False((await NotificacaoAsync())!.Value.GetProperty("lida").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await coord.PostAsync($"/api/comunicacoes/{id}/lida", null)).StatusCode);
        Assert.True((await NotificacaoAsync())!.Value.GetProperty("lida").GetBoolean());
    }

    [Fact]
    public async Task Destinatarios_nao_e_capturado_pela_rota_de_id()
    {
        var (ana, _, coordId, alunoId) = await CenarioAsync();
        var lista = await ana.GetFromJsonAsync<JsonElement>($"/api/comunicacoes/destinatarios?alunoId={alunoId}");
        Assert.Contains(lista.EnumerateArray(), d => d.GetProperty("id").GetGuid() == coordId);
    }
}
