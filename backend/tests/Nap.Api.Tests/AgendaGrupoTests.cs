using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nap.Api.Tests;

public class AgendaGrupoTests(ApiFactory fabrica)
{
    private static Task<HttpResponseMessage> Enviar(HttpClient c, HttpMethod m, string url, object corpo, string versao)
    {
        var req = new HttpRequestMessage(m, url) { Content = JsonContent.Create(corpo) };
        req.Headers.Add("If-Match", $"\"{versao}\"");
        return c.SendAsync(req);
    }

    // semana aleatória (testes em paralelo compartilham a agenda da Ana), sempre numa quarta-feira
    private static DateTimeOffset QuartaAleatoria() =>
        new DateTimeOffset(2031, 1, 1, 14, 0, 0, TimeSpan.Zero).AddDays(7 * Random.Shared.Next(0, 3000));

    private static async Task<JsonElement> InstanciaAsync(HttpClient cliente, Guid profissionalId, DateTimeOffset inicio, string titulo)
    {
        var segunda = DateOnly.FromDateTime(inicio.UtcDateTime.AddDays(-2));
        var semana = await cliente.GetFromJsonAsync<JsonElement>($"/api/agenda/semana?inicio={segunda:yyyy-MM-dd}&profissionalId={profissionalId}");
        return semana.GetProperty("compromissos").EnumerateArray().Single(x => x.GetProperty("titulo").GetString() == titulo);
    }

    [Fact]
    public async Task Grupo_lista_participantes_conflito_diz_quem_e_participante_nao_remarca_o_grupo()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var carlos = await fabrica.ClienteComo("carlos.mendes@nap.demo");
        var anaId = await ApiFactory.IdDoUsuarioAsync(ana);
        var carlosId = await ApiFactory.IdDoUsuarioAsync(carlos);
        var inicio = QuartaAleatoria();
        var titulo = "Reunião " + Guid.NewGuid().ToString("N")[..8];
        var corpo = new { tipo = "ReuniaoEquipe", titulo, inicio, fim = inicio.AddHours(1), participantesIds = new[] { anaId, carlosId } };

        Assert.Equal(HttpStatusCode.Created, (await ana.PostAsJsonAsync("/api/agenda", corpo)).StatusCode);

        var daCarlos = await InstanciaAsync(carlos, carlosId, inicio, titulo);
        Assert.NotEqual(JsonValueKind.Null, daCarlos.GetProperty("grupoId").ValueKind);
        Assert.Equal(new[] { anaId, carlosId }.Order(),
            daCarlos.GetProperty("participantes").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).Order());

        // Mesmo horário de novo: 409 com os itens de cada participante.
        var repetido = await ana.PostAsJsonAsync("/api/agenda", corpo);
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        var conflitos = (await repetido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("conflitos").EnumerateArray().ToList();
        Assert.Equal(new[] { anaId, carlosId }.Order(), conflitos.Select(c => c.GetProperty("profissionalId").GetGuid()).Order());
        Assert.Contains(conflitos, c => c.GetProperty("profissional").GetString() == "Carlos Mendes");

        var id = daCarlos.GetProperty("id").GetGuid();
        var versao = daCarlos.GetProperty("versao").GetString()!;
        var remarcar = await Enviar(carlos, HttpMethod.Post, $"/api/agenda/{id}/remarcar?escopo=Grupo",
            new { inicio = inicio.AddHours(2), fim = inicio.AddHours(3) }, versao);
        Assert.Equal(HttpStatusCode.Forbidden, remarcar.StatusCode);

        var cancelar = await Enviar(carlos, HttpMethod.Post, $"/api/agenda/{id}/cancelar", new { motivo = "Não poderei ir" }, versao);
        Assert.Equal(HttpStatusCode.NoContent, cancelar.StatusCode);
        var daAna = await InstanciaAsync(ana, anaId, inicio, titulo);
        Assert.Equal("Agendado", daAna.GetProperty("estado").GetString());
        Assert.Equal([anaId], daAna.GetProperty("participantes").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task Semana_inclui_item_de_domingo()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var anaId = await ApiFactory.IdDoUsuarioAsync(ana);
        var domingo = QuartaAleatoria().AddDays(4); // 14h UTC de domingo, ainda domingo no fuso institucional
        var titulo = "Lembrete " + Guid.NewGuid().ToString("N")[..8];
        var criado = await ana.PostAsJsonAsync("/api/agenda", new { tipo = "Lembrete", titulo, inicio = domingo, fim = domingo.AddMinutes(30) });
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);

        var segunda = DateOnly.FromDateTime(domingo.UtcDateTime.AddDays(-6));
        var semana = await ana.GetFromJsonAsync<JsonElement>($"/api/agenda/semana?inicio={segunda:yyyy-MM-dd}&profissionalId={anaId}");
        Assert.Contains(semana.GetProperty("compromissos").EnumerateArray(), x => x.GetProperty("titulo").GetString() == titulo);
    }

    [Fact]
    public async Task Tipo_sem_grupo_com_participantes_retorna_400()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var inicio = QuartaAleatoria();
        var resposta = await ana.PostAsJsonAsync("/api/agenda", new
        {
            tipo = "ElaboracaoPei", titulo = "PEI", inicio, fim = inicio.AddHours(1),
            participantesIds = new[] { await ApiFactory.IdDoUsuarioAsync(ana) },
        });
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.True((await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("participantesIds", out _));
    }

    [Theory]
    [InlineData("gestao@nap.demo")]
    [InlineData("sip@nap.demo")]
    public async Task Gestao_e_sip_nao_criam_compromisso_de_grupo(string email)
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var cliente = await fabrica.ClienteComo(email);
        var inicio = QuartaAleatoria();

        var resposta = await cliente.PostAsJsonAsync("/api/agenda", new
        {
            tipo = "ReuniaoEquipe", titulo = "Reunião", inicio, fim = inicio.AddHours(1),
            participantesIds = new[] { await ApiFactory.IdDoUsuarioAsync(ana) },
        });

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Participante_com_escopo_minha_so_cancela_remarcar_e_atualizar_retornam_400()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var carlos = await fabrica.ClienteComo("carlos.mendes@nap.demo");
        var anaId = await ApiFactory.IdDoUsuarioAsync(ana);
        var carlosId = await ApiFactory.IdDoUsuarioAsync(carlos);
        var inicio = QuartaAleatoria();
        var titulo = "Reunião " + Guid.NewGuid().ToString("N")[..8];
        await ana.PostAsJsonAsync("/api/agenda", new { tipo = "ReuniaoEquipe", titulo, inicio, fim = inicio.AddHours(1), participantesIds = new[] { anaId, carlosId } });
        var daCarlos = await InstanciaAsync(carlos, carlosId, inicio, titulo);
        var id = daCarlos.GetProperty("id").GetGuid();
        var versao = daCarlos.GetProperty("versao").GetString()!;

        var remarcar = await Enviar(carlos, HttpMethod.Post, $"/api/agenda/{id}/remarcar", new { inicio = inicio.AddHours(2), fim = inicio.AddHours(3) }, versao);
        Assert.Equal(HttpStatusCode.BadRequest, remarcar.StatusCode);
        Assert.True((await remarcar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("escopo", out _));
        var atualizar = await Enviar(carlos, HttpMethod.Put, $"/api/agenda/{id}", new { titulo = "Outro título" }, versao);
        Assert.Equal(HttpStatusCode.BadRequest, atualizar.StatusCode);

        // Quem criou o grupo edita pelo escopo do grupo.
        var daAna = await InstanciaAsync(ana, anaId, inicio, titulo);
        var pelaAna = await Enviar(ana, HttpMethod.Put, $"/api/agenda/{daAna.GetProperty("id").GetGuid()}?escopo=Grupo", new { titulo = "Pauta nova" },
            daAna.GetProperty("versao").GetString()!);
        Assert.Equal(HttpStatusCode.NoContent, pelaAna.StatusCode);
    }
}
