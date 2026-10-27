using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nap.Api.Tests;

public class IfMatchTests(ApiFactory fabrica)
{
    private static Task<HttpResponseMessage> Enviar(HttpClient c, HttpMethod m, string url, object corpo, string? versao,
        string? chave = null)
    {
        var req = new HttpRequestMessage(m, url) { Content = JsonContent.Create(corpo) };
        if (versao is not null) req.Headers.Add("If-Match", $"\"{versao}\"");
        if (chave is not null) req.Headers.Add("Idempotency-Key", chave);
        return c.SendAsync(req);
    }

    private static object CorpoAluno(string nome, JsonElement ficha) => new
    {
        nomeCompleto = nome, semestre = ficha.GetProperty("semestre").GetInt32(),
        necessidadesEducacionais = (string?)null, observacoesAcademicas = (string?)null,
        profissionalResponsavelId = ficha.GetProperty("profissionalId").GetGuid(), matriculaAtiva = true,
    };

    private static async Task<(Guid Id, JsonElement Ficha)> NovoAlunoAsync(HttpClient cliente)
    {
        var (id, _) = await ApiFactory.CriarAlunoAsync(cliente, await ApiFactory.IdDoUsuarioAsync(cliente));
        return (id, await cliente.GetFromJsonAsync<JsonElement>($"/api/alunos/{id}"));
    }

    // semana aleatória (testes em paralelo compartilham a agenda da Ana), sempre numa quarta-feira
    private static DateTimeOffset QuartaAleatoria() =>
        new DateTimeOffset(2031, 1, 1, 14, 0, 0, TimeSpan.Zero).AddDays(7 * Random.Shared.Next(0, 3000));

    private static async Task<JsonElement> CompromissoAsync(HttpClient cliente, Guid profissionalId, DateTimeOffset inicio, Guid id)
    {
        var segunda = DateOnly.FromDateTime(inicio.UtcDateTime.AddDays(-2));
        var semana = await cliente.GetFromJsonAsync<JsonElement>($"/api/agenda/semana?inicio={segunda:yyyy-MM-dd}&profissionalId={profissionalId}");
        return semana.GetProperty("compromissos").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
    }

    private static async Task<(Guid Id, DateTimeOffset Inicio, Guid ProfissionalId)> NovoCompromissoAsync(HttpClient cliente)
    {
        var profissionalId = await ApiFactory.IdDoUsuarioAsync(cliente);
        var (alunoId, _) = await ApiFactory.CriarAlunoAsync(cliente, profissionalId);
        var inicio = QuartaAleatoria();
        var resposta = await cliente.PostAsJsonAsync("/api/agenda",
            new { tipo = "Atendimento", titulo = "Atendimento", inicio, fim = inicio.AddHours(1), alunoId });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return ((await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(), inicio, profissionalId);
    }

    [Fact]
    public async Task Edicao_de_aluno_com_etag_fraca_e_aceita()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var (id, ficha) = await NovoAlunoAsync(cliente);
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/alunos/{id}") { Content = JsonContent.Create(CorpoAluno("Etag Fraca", ficha)) };
        req.Headers.TryAddWithoutValidation("If-Match", $"W/\"{ficha.GetProperty("versao").GetString()}\"");

        var resposta = await cliente.SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
    }

    [Fact]
    public async Task Edicao_de_aluno_com_if_match_malformado_retorna_400_e_nao_altera()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var (id, ficha) = await NovoAlunoAsync(cliente);
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/alunos/{id}") { Content = JsonContent.Create(CorpoAluno("If-Match Invalido", ficha)) };
        req.Headers.TryAddWithoutValidation("If-Match", "\"abc\"");

        var resposta = await cliente.SendAsync(req);

        // Cabeçalho inválido é erro do cliente (400 no campo ifMatch), não conflito de concorrência (409).
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("ifMatch", await resposta.Content.ReadAsStringAsync());
        var depois = await cliente.GetFromJsonAsync<JsonElement>($"/api/alunos/{id}");
        Assert.Equal(ficha.GetProperty("nome").GetString(), depois.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Edicao_de_aluno_sem_if_match_retorna_428_e_nao_altera()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var (id, ficha) = await NovoAlunoAsync(cliente);

        var resposta = await Enviar(cliente, HttpMethod.Put, $"/api/alunos/{id}", CorpoAluno("Sem Cabecalho", ficha), null);

        Assert.Equal(HttpStatusCode.PreconditionRequired, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var depois = await cliente.GetFromJsonAsync<JsonElement>($"/api/alunos/{id}");
        Assert.Equal(ficha.GetProperty("nome").GetString(), depois.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Edicao_de_aluno_sem_matricula_ativa_retorna_400_e_nao_inativa()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var (id, ficha) = await NovoAlunoAsync(cliente);
        var semCampo = new
        {
            nomeCompleto = "Sem Matricula Ativa", semestre = ficha.GetProperty("semestre").GetInt32(),
            necessidadesEducacionais = (string?)null, observacoesAcademicas = (string?)null,
            profissionalResponsavelId = ficha.GetProperty("profissionalId").GetGuid(),
        };

        var resposta = await Enviar(cliente, HttpMethod.Put, $"/api/alunos/{id}", semCampo, ficha.GetProperty("versao").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var depois = await cliente.GetFromJsonAsync<JsonElement>($"/api/alunos/{id}");
        Assert.True(depois.GetProperty("matriculaAtiva").GetBoolean());
    }

    [Fact]
    public async Task Segunda_aba_com_versao_antiga_recebe_409_e_prevalece_a_primeira_edicao()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var (id, ficha) = await NovoAlunoAsync(cliente);
        var v1 = ficha.GetProperty("versao").GetString()!;

        var primeira = await Enviar(cliente, HttpMethod.Put, $"/api/alunos/{id}", CorpoAluno("Primeira Aba", ficha), v1);
        var segunda = await Enviar(cliente, HttpMethod.Put, $"/api/alunos/{id}", CorpoAluno("Segunda Aba", ficha), v1);

        Assert.Equal(HttpStatusCode.NoContent, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        var depois = await cliente.GetFromJsonAsync<JsonElement>($"/api/alunos/{id}");
        Assert.Equal("Primeira Aba", depois.GetProperty("nome").GetString());
        Assert.NotEqual(v1, depois.GetProperty("versao").GetString());
    }

    [Fact]
    public async Task Cancelar_compromisso_com_versao_antiga_retorna_409_e_mantem_agendado()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var (id, inicio, profissionalId) = await NovoCompromissoAsync(cliente);
        var v1 = (await CompromissoAsync(cliente, profissionalId, inicio, id)).GetProperty("versao").GetString()!;

        var edicao = await Enviar(cliente, HttpMethod.Put, $"/api/agenda/{id}", new { titulo = "Novo título", descricao = (string?)null }, v1);
        var cancelamento = await Enviar(cliente, HttpMethod.Post, $"/api/agenda/{id}/cancelar", new { motivo = "Imprevisto" }, v1);

        Assert.Equal(HttpStatusCode.NoContent, edicao.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, cancelamento.StatusCode);
        var atual = await CompromissoAsync(cliente, profissionalId, inicio, id);
        Assert.Equal("Agendado", atual.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Remarcar_repetido_com_mesma_chave_devolve_o_mesmo_id()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var (id, inicio, profissionalId) = await NovoCompromissoAsync(cliente);
        var versao = (await CompromissoAsync(cliente, profissionalId, inicio, id)).GetProperty("versao").GetString()!;
        var chave = Guid.NewGuid().ToString();
        var corpo = new { inicio = inicio.AddDays(1), fim = inicio.AddDays(1).AddHours(1) };

        var primeira = await Enviar(cliente, HttpMethod.Post, $"/api/agenda/{id}/remarcar", corpo, versao, chave);
        var repetida = await Enviar(cliente, HttpMethod.Post, $"/api/agenda/{id}/remarcar", corpo, versao, chave);

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Created, repetida.StatusCode);
        Assert.Equal((await primeira.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
            (await repetida.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Correcao_de_atendimento_com_versao_antiga_retorna_409()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var (alunoId, _) = await ApiFactory.CriarAlunoAsync(cliente, await ApiFactory.IdDoUsuarioAsync(cliente));
        // atendimento só pode ser registrado depois de terminar: quarta-feira aleatória no passado
        var inicio = new DateTimeOffset(2020, 1, 1, 14, 0, 0, TimeSpan.Zero).AddDays(7 * Random.Shared.Next(0, 300));
        var registro = await cliente.PostAsJsonAsync("/api/atendimentos", new
        {
            alunoId, inicio, fim = inicio.AddHours(1), tipo = "Acompanhamento", estado = "Ausente",
            justificativaAvulso = "Atendimento avulso de teste",
        });
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        var id = (await registro.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var v1 = (await cliente.GetFromJsonAsync<JsonElement>($"/api/atendimentos/{id}")).GetProperty("versao").GetString()!;
        var correcao = new { estado = "Ausente", tipo = "Devolutiva", inicio, fim = inicio.AddHours(1) };

        var primeira = await Enviar(cliente, HttpMethod.Put, $"/api/atendimentos/{id}", correcao, v1);
        var segunda = await Enviar(cliente, HttpMethod.Put, $"/api/atendimentos/{id}", correcao, v1);

        Assert.Equal(HttpStatusCode.NoContent, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task Coordenador_de_curso_recebe_403_antes_da_checagem_de_versao()
    {
        var coordenador = await fabrica.ClienteComo("coord.direito@nap.demo");
        var lista = await coordenador.GetFromJsonAsync<JsonElement>("/api/alunos");
        var id = lista.GetProperty("itens").EnumerateArray().First().GetProperty("id").GetGuid();
        var ficha = await coordenador.GetFromJsonAsync<JsonElement>($"/api/alunos/{id}");
        var versao = ficha.GetProperty("versao").GetString()!;
        var corpo = new { nomeCompleto = "Nao Pode", semestre = 1, profissionalResponsavelId = Guid.NewGuid() };

        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(coordenador, HttpMethod.Put, $"/api/alunos/{id}", corpo, versao)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(coordenador, HttpMethod.Put, $"/api/alunos/{id}", corpo, null)).StatusCode);
    }
}
