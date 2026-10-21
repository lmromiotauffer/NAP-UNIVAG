using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nap.Api.Tests;

public class AdministracaoTests(ApiFactory fabrica)
{
    private const string NovaSenha = "NovaSenha2026x";

    private static HttpRequestMessage ComIfMatch(HttpMethod metodo, string caminho, string versao, object? corpo = null)
    {
        var req = new HttpRequestMessage(metodo, caminho) { Content = JsonContent.Create(corpo ?? new { }) };
        req.Headers.Add("If-Match", $"\"{versao}\"");
        return req;
    }

    private static async Task<string> VersaoDeAsync(HttpClient cliente, string caminho) =>
        (await cliente.GetFromJsonAsync<JsonElement>(caminho)).GetProperty("versao").GetString()!;

    /// <summary>Cria um usuário pela API (e-mail único) e devolve o login com a senha temporária.</summary>
    private static async Task<(string Email, Guid Id, string Senha)> CriarUsuarioAsync(HttpClient coordenacao, string perfil = "ProfissionalNap")
    {
        var email = $"adm.{Guid.NewGuid():N}@nap.teste";
        var resposta = await coordenacao.PostAsJsonAsync("/api/usuarios",
            new { email, nomeCompleto = "Usuário Administrado", perfil, cursoId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return (email, corpo.GetProperty("usuarioId").GetGuid(), corpo.GetProperty("senhaTemporaria").GetString()!);
    }

    /// <summary>Quarta-feira num ano aleatório entre 2100 e 2900, às 11h no fuso institucional (14h UTC).</summary>
    private static DateTimeOffset QuartaDistante()
    {
        var primeira = new DateTimeOffset(2100, 1, 6, 14, 0, 0, TimeSpan.Zero); // quarta-feira
        return primeira.AddDays(7 * Random.Shared.Next(0, 40_000));
    }

    [Fact]
    public async Task Coordenacao_nap_cria_profissional_e_recebe_senha_uma_vez()
    {
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        var email = $"adm.{Guid.NewGuid():N}@nap.teste";

        var resposta = await coordenacao.PostAsJsonAsync("/api/usuarios",
            new { email, nomeCompleto = "Profissional Novo", perfil = "ProfissionalNap", cursoId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Contains("no-store", resposta.Headers.CacheControl?.ToString());
        var criado = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(16, criado.GetProperty("senhaTemporaria").GetString()!.Length);
        var id = criado.GetProperty("usuarioId").GetGuid();
        var detalhe = await coordenacao.GetStringAsync($"/api/usuarios/{id}");
        Assert.DoesNotContain("senhaTemporaria", detalhe, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Coordenacao_nap_recebe_403_ao_tocar_administrador()
    {
        var admin = await fabrica.ClienteComo("admin@nap.demo");
        var adminId = await ApiFactory.IdDoUsuarioAsync(admin);
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        // Leitura é permitida; a alçada restringe só as escritas.
        var versao = await VersaoDeAsync(coordenacao, $"/api/usuarios/{adminId}");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await coordenacao.SendAsync(ComIfMatch(HttpMethod.Post, $"/api/usuarios/{adminId}/situacao", versao, new { ativo = false }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await coordenacao.PostAsync($"/api/usuarios/{adminId}/senha-temporaria", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await coordenacao.PostAsJsonAsync("/api/usuarios",
            new { email = $"adm.{Guid.NewGuid():N}@nap.teste", nomeCompleto = "Outro Administrador", perfil = "Administrador", cursoId = (Guid?)null })).StatusCode);
    }

    [Fact]
    public async Task Ninguem_altera_o_proprio_perfil()
    {
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        var id = await ApiFactory.IdDoUsuarioAsync(coordenacao);
        var versao = await VersaoDeAsync(coordenacao, $"/api/usuarios/{id}");

        var resposta = await coordenacao.SendAsync(ComIfMatch(HttpMethod.Put, $"/api/usuarios/{id}", versao,
            new { nomeCompleto = "Coordenação NAP", perfil = "ProfissionalNap", cursoId = (Guid?)null }));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Coordenacao_nap_recebe_403_ao_atribuir_perfil_administrador()
    {
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        var (_, id, _) = await CriarUsuarioAsync(coordenacao);
        var versao = await VersaoDeAsync(coordenacao, $"/api/usuarios/{id}");

        var resposta = await coordenacao.SendAsync(ComIfMatch(HttpMethod.Put, $"/api/usuarios/{id}", versao,
            new { nomeCompleto = "Usuário Administrado", perfil = "Administrador", cursoId = (Guid?)null }));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    // Último administrador ativo (422) não tem teste de API: o admin do seed é o único e só ele mesmo o inativaria (403 de
    // autoalteração vem antes); criar outros administradores no banco compartilhado só tornaria a regra inalcançável.

    [Fact]
    public async Task Profissional_recebe_403_na_administracao()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");

        Assert.Equal(HttpStatusCode.Forbidden, (await ana.GetAsync("/api/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.PostAsJsonAsync("/api/cursos", new { nome = "Curso " + Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.GetAsync("/api/escalas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.GetAsync("/api/configuracoes/alertas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await ana.PostAsJsonAsync("/api/feriados", new { data = "2100-01-01", descricao = "Teste" })).StatusCode);
        // Listas abertas a todo autenticado continuam abertas.
        Assert.Equal(HttpStatusCode.OK, (await ana.GetAsync("/api/cursos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ana.GetAsync("/api/feriados?ano=2026")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ana.GetAsync("/api/usuarios/profissionais")).StatusCode);
    }

    [Fact]
    public async Task Inativar_usuario_derruba_a_sessao_dele()
    {
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        var (email, id, senha) = await CriarUsuarioAsync(coordenacao);
        var cliente = await ApiFactory.ClienteComo(fabrica, email, senha);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/auth/eu")).StatusCode);

        var versao = await VersaoDeAsync(coordenacao, $"/api/usuarios/{id}");
        var inativacao = await coordenacao.SendAsync(ComIfMatch(HttpMethod.Post, $"/api/usuarios/{id}/situacao", versao, new { ativo = false }));

        Assert.Equal(HttpStatusCode.NoContent, inativacao.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/auth/eu")).StatusCode);
    }

    [Fact]
    public async Task Novo_usuario_so_acessa_apos_trocar_a_senha()
    {
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        var (email, _, senha) = await CriarUsuarioAsync(coordenacao);
        var cliente = await ApiFactory.ClienteComo(fabrica, email, senha);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/cursos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/auth/eu")).StatusCode);

        var troca = await cliente.PostAsJsonAsync("/api/auth/senha", new { senhaAtual = senha, novaSenha = NovaSenha });
        Assert.Equal(HttpStatusCode.OK, troca.StatusCode);
        var sessao = await troca.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.GetProperty("accessToken").GetString());

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/cursos")).StatusCode);
    }

    [Fact]
    public async Task Feriado_retorna_conflitos()
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        var (alunoId, _) = await ApiFactory.CriarAlunoAsync(ana, await ApiFactory.IdDoUsuarioAsync(ana));
        var inicio = QuartaDistante();
        var compromisso = await ana.PostAsJsonAsync("/api/agenda",
            new { tipo = "Atendimento", titulo = "Atendimento", inicio, fim = inicio.AddHours(1), alunoId });
        Assert.Equal(HttpStatusCode.Created, compromisso.StatusCode);
        var compromissoId = (await compromisso.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var resposta = await coordenacao.PostAsJsonAsync("/api/feriados",
            new { data = DateOnly.FromDateTime(inicio.UtcDateTime).ToString("yyyy-MM-dd"), descricao = "Feriado de teste" });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        var conflito = Assert.Single(corpo.GetProperty("conflitos").EnumerateArray());
        Assert.Equal(compromissoId, conflito.GetProperty("id").GetGuid());
        // Nada foi alterado: o compromisso continua agendado.
        var semana = DateOnly.FromDateTime(inicio.UtcDateTime);
        semana = semana.AddDays(-(((int)semana.DayOfWeek + 6) % 7));
        Assert.Contains(compromissoId.ToString(), await ana.GetStringAsync($"/api/agenda/semana?inicio={semana:yyyy-MM-dd}"));
        // Remover = inativar: libera a data para novo feriado.
        var feriadoId = corpo.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await coordenacao.PostAsync($"/api/feriados/{feriadoId}/inativar", null)).StatusCode);
    }

    [Fact]
    public async Task Escala_sobreposta_retorna_409()
    {
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        var (_, profissionalId, _) = await CriarUsuarioAsync(coordenacao);
        var ano = Random.Shared.Next(2100, 2900);
        object Corpo(string inicio, string? fim) => new
        {
            profissionalId, vigenciaInicio = inicio, vigenciaFim = fim,
            faixas = new[] { new { dia = "Monday", inicio = "08:00:00", fim = "12:00:00", semAtendimento = false } },
        };

        var primeira = await coordenacao.PostAsJsonAsync("/api/escalas", Corpo($"{ano}-01-01", $"{ano}-06-30"));
        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        var sobreposta = await coordenacao.PostAsJsonAsync("/api/escalas", Corpo($"{ano}-06-01", null));

        Assert.Equal(HttpStatusCode.Conflict, sobreposta.StatusCode);
        var lista = await coordenacao.GetFromJsonAsync<JsonElement>($"/api/escalas?profissionalId={profissionalId}");
        Assert.Equal(1, lista.GetArrayLength());
    }

    [Fact]
    public async Task Atualizar_alertas_reflete_no_painel()
    {
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        var original = await coordenacao.GetFromJsonAsync<JsonElement>("/api/configuracoes/alertas");
        int Campo(string nome) => original.GetProperty(nome).GetInt32();
        object Corpo(int diasSemAtualizacao) => new
        {
            faltasConsecutivas = Campo("faltasConsecutivas"), faltasJanela = Campo("faltasJanela"), janelaDias = Campo("janelaDias"),
            diasSemAcolhimento = Campo("diasSemAcolhimento"), diasSemAtualizacao, diasPeiPendente = Campo("diasPeiPendente"),
        };

        var atualizada = await coordenacao.SendAsync(ComIfMatch(HttpMethod.Put, "/api/configuracoes/alertas",
            original.GetProperty("versao").GetString()!, Corpo(44)));
        try
        {
            Assert.Equal(HttpStatusCode.OK, atualizada.StatusCode);
            var nova = await atualizada.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(44, nova.GetProperty("diasSemAtualizacao").GetInt32());
            Assert.NotEqual(original.GetProperty("versao").GetString(), nova.GetProperty("versao").GetString());
            var painel = await coordenacao.GetFromJsonAsync<JsonElement>("/api/painel");
            Assert.Equal(44, painel.GetProperty("encaminhamentos").GetProperty("diasSemAtualizacao").GetInt32());
            // Versão antiga: conflito de concorrência.
            Assert.Equal(HttpStatusCode.Conflict, (await coordenacao.SendAsync(ComIfMatch(HttpMethod.Put, "/api/configuracoes/alertas",
                original.GetProperty("versao").GetString()!, Corpo(10)))).StatusCode);
        }
        finally
        {
            // Restaura o padrão: a configuração é única e compartilhada com os demais testes.
            var atual = await coordenacao.GetFromJsonAsync<JsonElement>("/api/configuracoes/alertas");
            var restauracao = await coordenacao.SendAsync(ComIfMatch(HttpMethod.Put, "/api/configuracoes/alertas",
                atual.GetProperty("versao").GetString()!, Corpo(Campo("diasSemAtualizacao"))));
            Assert.Equal(HttpStatusCode.OK, restauracao.StatusCode);
        }
    }

    [Fact]
    public async Task Trocar_responsavel_do_pei()
    {
        var (ana, alunoId, _, _) = await PeisTests.EmRevisaoAsync(fabrica);
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        var anaId = await ApiFactory.IdDoUsuarioAsync(ana);
        var rafael = await fabrica.ClienteComo("rafael.torres@nap.demo");
        var rafaelId = await ApiFactory.IdDoUsuarioAsync(rafael);
        var peis = await coordenacao.GetFromJsonAsync<JsonElement>($"/api/alunos/{alunoId}/peis");
        var peiId = peis[0].GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await ana.PostAsJsonAsync($"/api/peis/{peiId}/responsavel",
            new { novoResponsavelId = rafaelId, justificativa = "Afastamento da responsável" })).StatusCode);
        var resposta = await coordenacao.PostAsJsonAsync($"/api/peis/{peiId}/responsavel",
            new { novoResponsavelId = rafaelId, justificativa = "Afastamento da responsável" });

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var depois = await coordenacao.GetFromJsonAsync<JsonElement>($"/api/alunos/{alunoId}/peis");
        Assert.NotEqual(anaId, rafaelId);
        Assert.Contains("Rafael", depois[0].GetProperty("responsavel").GetString());
    }
}
