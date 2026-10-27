using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nap.Api.Tests;

public class AtividadesTests(ApiFactory fabrica)
{
    // Datas passadas e aleatórias: o banco de teste é compartilhado e atividades ocupam o horário do profissional.
    // Dia útil entre 07h e 14h no fuso institucional (UTC-4): a agenda recusa início fora da escala padrão (seg a sex, 07h a 17h).
    private static DateTimeOffset InicioAleatorio()
    {
        var inicio = new DateTimeOffset(2012, 1, 1, 11, 0, 0, TimeSpan.Zero).AddDays(Random.Shared.Next(0, 4000)).AddHours(Random.Shared.Next(0, 8));
        while (inicio.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) inicio = inicio.AddDays(1);
        return inicio;
    }

    private static async Task<Guid> CompromissoAsync(HttpClient cliente, DateTimeOffset inicio, string titulo)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/agenda",
            new { tipo = "AtividadeCurso", titulo, inicio, fim = inicio.AddHours(1) });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static object Corpo(Guid? compromissoId, DateTimeOffset inicio, string descricao, Guid? cursoId = null) => new
    {
        compromissoId, inicio, fim = inicio.AddHours(1), cursoId, tipo = "RodaConversa", descricao,
        justificativaAvulso = compromissoId is null ? "Atividade não agendada" : null,
    };

    private static Task<HttpResponseMessage> Enviar(HttpClient c, HttpMethod m, string url, object corpo, string? versao = null,
        string? chave = null)
    {
        var req = new HttpRequestMessage(m, url) { Content = JsonContent.Create(corpo) };
        if (versao is not null) req.Headers.Add("If-Match", $"\"{versao}\"");
        if (chave is not null) req.Headers.Add("Idempotency-Key", chave);
        return c.SendAsync(req);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

    private static async Task<Guid> CursoDireitoAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<JsonElement>("/api/cursos")).EnumerateArray()
            .First(c => c.GetProperty("nome").GetString() == "Direito").GetProperty("id").GetGuid();

    [Fact]
    public async Task Registrar_de_compromisso_conclui_e_aparece_na_lista()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var inicio = InicioAleatorio();
        var descricao = "Roda " + Guid.NewGuid().ToString("N");
        var compromissoId = await CompromissoAsync(cliente, inicio, "Atividade " + descricao);

        var resposta = await Enviar(cliente, HttpMethod.Post, "/api/atividades", Corpo(compromissoId, inicio, descricao));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var id = await IdAsync(resposta);
        Assert.EndsWith($"/api/atividades/{id}", resposta.Headers.Location!.ToString());
        var lista = await cliente.GetFromJsonAsync<JsonElement>($"/api/atividades?inicio={inicio:yyyy-MM-dd}&fim={inicio:yyyy-MM-dd}");
        Assert.Contains(lista.GetProperty("itens").EnumerateArray(), a => a.GetProperty("id").GetGuid() == id);
        var detalhe = await cliente.GetFromJsonAsync<JsonElement>($"/api/atividades/{id}");
        Assert.Equal(compromissoId, detalhe.GetProperty("compromissoId").GetGuid());
        Assert.True(detalhe.GetProperty("podeCorrigir").GetBoolean());
    }

    [Fact]
    public async Task Mesma_chave_de_idempotencia_nao_duplica()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var inicio = InicioAleatorio();
        var corpo = Corpo(null, inicio, "Avulsa " + Guid.NewGuid().ToString("N"));
        var chave = Guid.NewGuid().ToString();

        var primeira = await Enviar(cliente, HttpMethod.Post, "/api/atividades", corpo, chave: chave);
        var segunda = await Enviar(cliente, HttpMethod.Post, "/api/atividades", corpo, chave: chave);

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);
        Assert.Equal(await IdAsync(primeira), await IdAsync(segunda));
        var lista = await cliente.GetFromJsonAsync<JsonElement>($"/api/atividades?inicio={inicio:yyyy-MM-dd}&fim={inicio:yyyy-MM-dd}");
        Assert.Single(lista.GetProperty("itens").EnumerateArray());
    }

    [Fact]
    public async Task Coordenador_de_curso_recebe_404_em_institucional()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var admin = await fabrica.ClienteComo("admin@nap.demo");
        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");
        var cursoId = await CursoDireitoAsync(admin);
        var inicio = InicioAleatorio();
        var institucional = await IdAsync(await Enviar(ana, HttpMethod.Post, "/api/atividades", Corpo(null, inicio, "Institucional")));
        // Logo após a primeira (horários consecutivos são permitidos): +3h chegaria às 17h locais, fora da escala padrão.
        var doCurso = await IdAsync(await Enviar(ana, HttpMethod.Post, "/api/atividades",
            Corpo(null, inicio.AddHours(1), "Do curso", cursoId)));

        Assert.Equal(HttpStatusCode.NotFound, (await coord.GetAsync($"/api/atividades/{institucional}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coord.GetAsync($"/api/atividades/{doCurso}")).StatusCode);
        var lista = await coord.GetFromJsonAsync<JsonElement>($"/api/atividades?inicio={inicio:yyyy-MM-dd}&fim={inicio:yyyy-MM-dd}");
        var ids = lista.GetProperty("itens").EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(doCurso, ids);
        Assert.DoesNotContain(institucional, ids);
    }

    [Theory]
    [InlineData("gestao@nap.demo")]
    [InlineData("sip@nap.demo")]
    public async Task Gestao_e_sip_recebem_403(string email)
    {
        var cliente = await fabrica.ClienteComo(email);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/atividades")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/atividades/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Enviar(cliente, HttpMethod.Post, "/api/atividades", Corpo(null, InicioAleatorio(), "Nao"))).StatusCode);
    }

    [Fact]
    public async Task Corrigir_sem_if_match_retorna_428()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var inicio = InicioAleatorio();
        var id = await IdAsync(await Enviar(cliente, HttpMethod.Post, "/api/atividades", Corpo(null, inicio, "Para corrigir")));
        var correcao = new { inicio, fim = inicio.AddHours(1), cursoId = (Guid?)null, tipo = "Outra", descricao = "Corrigida",
            justificativa = "Ajuste de tipo" };

        var sem = await Enviar(cliente, HttpMethod.Put, $"/api/atividades/{id}", correcao);
        Assert.Equal(HttpStatusCode.PreconditionRequired, sem.StatusCode);

        var versao = (await cliente.GetFromJsonAsync<JsonElement>($"/api/atividades/{id}")).GetProperty("versao").GetString();
        var com = await Enviar(cliente, HttpMethod.Put, $"/api/atividades/{id}", correcao, versao);
        Assert.Equal(HttpStatusCode.OK, com.StatusCode);
        Assert.Equal("Outra", (await com.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tipo").GetString());
    }
}
