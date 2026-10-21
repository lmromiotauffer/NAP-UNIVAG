using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Nap.Infrastructure.Identidade;

namespace Nap.Api.Tests;

public class ProducaoTests(ApiFactory fabrica)
{
    // Semanas de anos distantes e aleatórios (2001 a 2009): o banco de teste é compartilhado e fechar/ajustar não se desfaz.
    private static DateOnly SegundaAleatoria()
    {
        var dia = new DateOnly(2001, 1, 1).AddDays(Random.Shared.Next(0, 3000));
        return dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));
    }

    private static Task<HttpResponseMessage> Enviar(HttpClient c, string url, object corpo, string? versao = null, string? chave = null,
        HttpMethod? metodo = null)
    {
        var req = new HttpRequestMessage(metodo ?? HttpMethod.Post, url) { Content = JsonContent.Create(corpo) };
        if (versao is not null) req.Headers.Add("If-Match", $"\"{versao}\"");
        if (chave is not null) req.Headers.Add("Idempotency-Key", chave);
        return c.SendAsync(req);
    }

    private static object Ajuste(Guid profissionalId, DateOnly data) => new
    {
        profissionalId, data, categoria = "AtendimentosRealizados", ocorrencias = 3, minutos = 150, justificativa = "Atendimentos fora do sistema",
    };

    /// <summary>Coordenação nova, para que fechar semanas não afete os usuários do seed.</summary>
    private async Task<(HttpClient Cliente, Guid Id)> ProfissionalNovoAsync()
    {
        var (email, id) = await ApiFactory.CriarUsuarioAsync(fabrica);
        return (await ApiFactory.ClienteComo(fabrica, email), id);
    }

    private static async Task<JsonElement> SemanaAsync(HttpClient c, Guid profissionalId, DateOnly segunda) =>
        (await c.GetFromJsonAsync<JsonElement>($"/api/producao/semanas?profissionalId={profissionalId}&inicio={segunda:yyyy-MM-dd}&fim={segunda:yyyy-MM-dd}"))
        .EnumerateArray().Single();

    [Fact]
    public async Task Ajuste_em_semana_fechada_retorna_422()
    {
        var (cliente, id) = await ProfissionalNovoAsync();
        var admin = await fabrica.ClienteComo("admin@nap.demo");
        var segunda = SegundaAleatoria();
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(cliente, "/api/producao/semanas/fechar",
            new { profissionalId = id, semana = segunda, confirmarPendencias = false })).StatusCode);

        var resposta = await Enviar(admin, "/api/producao/ajustes", Ajuste(id, segunda.AddDays(2)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Empty((await admin.GetFromJsonAsync<JsonElement>(
            $"/api/producao/ajustes?profissionalId={id}&inicio={segunda:yyyy-MM-dd}&fim={segunda.AddDays(6):yyyy-MM-dd}")).EnumerateArray());
    }

    [Fact]
    public async Task Profissional_nao_fecha_semana_de_outro_profissional()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var carlosId = await ApiFactory.IdDoUsuarioAsync(await fabrica.ClienteComo("carlos.mendes@nap.demo"));

        var resposta = await Enviar(ana, "/api/producao/semanas/fechar",
            new { profissionalId = carlosId, semana = SegundaAleatoria(), confirmarPendencias = false });

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Profissional_nao_lanca_ajuste()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var id = await ApiFactory.IdDoUsuarioAsync(ana);

        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(ana, "/api/producao/ajustes", Ajuste(id, SegundaAleatoria()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.GetAsync("/api/producao/ajustes?inicio=2001-01-01&fim=2001-01-31")).StatusCode);
    }

    [Fact]
    public async Task Producao_do_profissional_via_api()
    {
        var admin = await fabrica.ClienteComo("admin@nap.demo");
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var carlos = await fabrica.ClienteComo("carlos.mendes@nap.demo");
        var anaId = await ApiFactory.IdDoUsuarioAsync(ana);
        var carlosId = await ApiFactory.IdDoUsuarioAsync(carlos);
        var segunda = SegundaAleatoria();
        var chave = Guid.NewGuid().ToString();

        var criado = await Enviar(admin, "/api/producao/ajustes", Ajuste(anaId, segunda.AddDays(1)), chave: chave);
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        var id = (await criado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var repetido = await Enviar(admin, "/api/producao/ajustes", Ajuste(anaId, segunda.AddDays(1)), chave: chave);
        Assert.Equal(HttpStatusCode.Created, repetido.StatusCode);
        Assert.Equal(criado.Headers.Location, repetido.Headers.Location);

        // O filtro de outro profissional é ignorado: a resposta é sempre a produção da Ana.
        var producao = await ana.GetFromJsonAsync<JsonElement>($"/api/producao?tipo=Semana&referencia={segunda:yyyy-MM-dd}&profissionalId={carlosId}");
        var atendimentos = producao.GetProperty("categorias").EnumerateArray().First(c => c.GetProperty("chave").GetString() == "AtendimentosRealizados");
        Assert.Equal(3, atendimentos.GetProperty("ajustes").GetProperty("ocorrencias").GetInt32());
        Assert.Equal(150, atendimentos.GetProperty("total").GetProperty("minutos").GetInt32());
        Assert.Equal(JsonValueKind.Null, producao.GetProperty("porProfissional").ValueKind);

        var lista = (await admin.GetFromJsonAsync<JsonElement>(
            $"/api/producao/ajustes?profissionalId={anaId}&inicio={segunda:yyyy-MM-dd}&fim={segunda.AddDays(6):yyyy-MM-dd}")).EnumerateArray().Single();
        Assert.Equal(id, lista.GetProperty("id").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(lista.GetProperty("autor").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(lista.GetProperty("profissional").GetString()));
    }

    [Fact]
    public async Task Coordenador_de_curso_recebe_so_indicadores_do_curso()
    {
        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");

        var producao = await coord.GetFromJsonAsync<JsonElement>("/api/producao?tipo=Mes&referencia=2020-03-10");

        Assert.Equal(3, producao.GetProperty("categorias").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, producao.GetProperty("tempo").ValueKind);
        Assert.Equal(JsonValueKind.Null, producao.GetProperty("pendentes").ValueKind);
        Assert.Equal(JsonValueKind.Null, producao.GetProperty("porProfissional").ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, (await coord.GetAsync("/api/producao/semanas?inicio=2020-03-01&fim=2020-03-31")).StatusCode);
    }

    [Fact]
    public async Task Sip_recebe_403()
    {
        var sip = await fabrica.ClienteComo("sip@nap.demo");

        Assert.Equal(HttpStatusCode.Forbidden, (await sip.GetAsync("/api/producao?tipo=Mes&referencia=2020-03-10")).StatusCode);
    }

    [Fact]
    public async Task Gestao_consulta_mas_nao_gere()
    {
        var gestao = await fabrica.ClienteComo("gestao@nap.demo");

        Assert.Equal(HttpStatusCode.OK, (await gestao.GetAsync("/api/producao?tipo=Mes&referencia=2020-03-10")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await gestao.GetAsync("/api/producao/semanas?inicio=2020-03-01&fim=2020-03-31")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(gestao, "/api/producao/ajustes", Ajuste(Guid.NewGuid(), SegundaAleatoria()))).StatusCode);
    }

    [Fact]
    public async Task Fechar_e_reabrir_via_api()
    {
        var (cliente, id) = await ProfissionalNovoAsync();
        var admin = await fabrica.ClienteComo("admin@nap.demo");
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var segunda = SegundaAleatoria();

        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(cliente, "/api/producao/semanas/fechar",
            new { profissionalId = id, semana = segunda, confirmarPendencias = false })).StatusCode);
        var fechada = await SemanaAsync(cliente, id, segunda);
        Assert.True(fechada.GetProperty("fechada").GetBoolean());
        var versao = fechada.GetProperty("versao").GetString()!;
        var corpo = new { profissionalId = id, semana = segunda, justificativa = "Faltou lançar um atendimento" };

        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(ana, "/api/producao/semanas/reabrir", corpo, versao)).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await Enviar(admin, "/api/producao/semanas/reabrir", corpo)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(admin, "/api/producao/semanas/reabrir", corpo, versao)).StatusCode);

        var reaberta = await SemanaAsync(admin, id, segunda);
        Assert.False(reaberta.GetProperty("fechada").GetBoolean());
        Assert.Equal(2, reaberta.GetProperty("eventos").GetArrayLength());
    }

    [Fact]
    public async Task Realizacao_via_api_exige_if_match()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var anaId = await ApiFactory.IdDoUsuarioAsync(ana);
        // Dia útil passado, 07h a 14h no fuso institucional (UTC-4), como nos demais testes que ocupam a agenda.
        var inicio = new DateTimeOffset(2012, 1, 1, 11, 0, 0, TimeSpan.Zero).AddDays(Random.Shared.Next(0, 4000)).AddHours(Random.Shared.Next(0, 8));
        while (inicio.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) inicio = inicio.AddDays(1);
        var criado = await ana.PostAsJsonAsync("/api/agenda",
            new { tipo = "ReuniaoEquipe", titulo = "Reunião " + Guid.NewGuid().ToString("N"), inicio, fim = inicio.AddHours(1) });
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        var id = (await criado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var corpo = new { inicio, fim = inicio.AddMinutes(45) };

        Assert.Equal(HttpStatusCode.PreconditionRequired, (await Enviar(ana, $"/api/agenda/{id}/realizacao", corpo)).StatusCode);

        var dia = DateOnly.FromDateTime(inicio.UtcDateTime.AddHours(-4));
        var segunda = dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));
        async Task<JsonElement> CompromissoAsync() =>
            (await ana.GetFromJsonAsync<JsonElement>($"/api/agenda/semana?inicio={segunda:yyyy-MM-dd}&profissionalId={anaId}"))
            .GetProperty("compromissos").EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == id);
        var versao = (await CompromissoAsync()).GetProperty("versao").GetString()!;
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(ana, $"/api/agenda/{id}/realizacao", corpo, versao)).StatusCode);

        // Correção do horário efetivo: PUT com If-Match e justificativa.
        var correcao = new { inicio, fim = inicio.AddMinutes(30), justificativa = "Horário anotado errado" };
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await Enviar(ana, $"/api/agenda/{id}/realizacao", correcao, metodo: HttpMethod.Put)).StatusCode);
        versao = (await CompromissoAsync()).GetProperty("versao").GetString()!;
        Assert.Equal(HttpStatusCode.NoContent,
            (await Enviar(ana, $"/api/agenda/{id}/realizacao", correcao, versao, metodo: HttpMethod.Put)).StatusCode);
        Assert.Equal(inicio.AddMinutes(30), (await CompromissoAsync()).GetProperty("fim").GetDateTimeOffset());
    }

    [Fact]
    public async Task Profissionais_com_inativos_so_para_coordenacao_e_gestao()
    {
        var (_, inativoId) = await ApiFactory.CriarUsuarioAsync(fabrica);
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentidade>>();
            var u = (await usuarios.FindByIdAsync(inativoId.ToString()))!;
            u.Ativo = false;
            Assert.True((await usuarios.UpdateAsync(u)).Succeeded);
        }
        async Task<JsonElement?> Inativo(string email, string url) =>
            (await (await fabrica.ClienteComo(email)).GetFromJsonAsync<JsonElement>(url)).EnumerateArray()
            .Cast<JsonElement?>().SingleOrDefault(p => p!.Value.GetProperty("id").GetGuid() == inativoId);

        var paraCoordenacao = await Inativo("coordenacao@nap.demo", "/api/usuarios/profissionais?incluirInativos=true");
        Assert.False(paraCoordenacao!.Value.GetProperty("ativo").GetBoolean());
        Assert.NotNull(await Inativo("gestao@nap.demo", "/api/usuarios/profissionais?incluirInativos=true"));
        Assert.Null(await Inativo("ana.beatriz@nap.demo", "/api/usuarios/profissionais?incluirInativos=true"));
        Assert.Null(await Inativo("coordenacao@nap.demo", "/api/usuarios/profissionais"));
    }
}
