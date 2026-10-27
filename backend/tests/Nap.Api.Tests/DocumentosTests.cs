using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Nap.Api.Tests;

public class DocumentosTests(ApiFactory fabrica)
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF\n");

    private static MultipartFormDataContent Formulario(byte[] conteudo, string nomeArquivo, string categoria = "Laudo",
        string descricao = "Laudo do aluno")
    {
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent
        {
            { arquivo, "arquivo", nomeArquivo },
            { new StringContent(categoria), "categoria" },
            { new StringContent(descricao), "descricao" },
        };
    }

    private static async Task<Guid> AnexarAsync(HttpClient cliente, Guid alunoId, byte[]? conteudo = null, string nome = "laudo.pdf")
    {
        var resposta = await cliente.PostAsync($"/api/alunos/{alunoId}/documentos", Formulario(conteudo ?? Pdf, nome));
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> AcaoAsync(HttpClient cliente, HttpMethod metodo, Guid id, string acao, object corpo)
    {
        var versao = (await cliente.GetFromJsonAsync<JsonElement>($"/api/documentos/{id}")).GetProperty("versao").GetString();
        var req = new HttpRequestMessage(metodo, $"/api/documentos/{id}/{acao}") { Content = JsonContent.Create(corpo) };
        req.Headers.Add("If-Match", $"\"{versao}\"");
        return await cliente.SendAsync(req);
    }

    private static Task<HttpResponseMessage> CompartilharAsync(HttpClient cliente, Guid id) =>
        AcaoAsync(cliente, HttpMethod.Post, id, "compartilhar", new { justificativa = "Apoio pedagógico" });

    private async Task<(HttpClient Ana, Guid AlunoId)> CenarioAsync(string curso = "Direito")
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var (alunoId, _) = await ApiFactory.CriarAlunoAsync(ana, await ApiFactory.IdDoUsuarioAsync(ana), curso);
        return (ana, alunoId);
    }

    private int ArquivosGravados() =>
        Directory.Exists(fabrica.DiretorioArquivos) ? Directory.GetFiles(fabrica.DiretorioArquivos, "*", SearchOption.AllDirectories).Length : 0;

    [Fact]
    public async Task Upload_e_download_preservam_bytes_e_nome()
    {
        var (ana, alunoId) = await CenarioAsync();
        var id = await AnexarAsync(ana, alunoId, nome: "relatório médico.pdf");

        var dto = await ana.GetFromJsonAsync<JsonElement>($"/api/documentos/{id}");
        Assert.Equal("relatório médico.pdf", dto.GetProperty("nomeArquivo").GetString());
        var lista = await ana.GetFromJsonAsync<JsonElement>($"/api/alunos/{alunoId}/documentos");
        Assert.Equal(1, lista.GetProperty("total").GetInt32());

        var resposta = await ana.GetAsync($"/api/documentos/{id}/arquivo");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(Pdf, await resposta.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", resposta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("relatório médico.pdf", resposta.Content.Headers.ContentDisposition?.FileNameStar);
    }

    [Fact]
    public async Task Download_tem_nosniff_e_attachment()
    {
        var (ana, alunoId) = await CenarioAsync();
        var id = await AnexarAsync(ana, alunoId);

        var resposta = await ana.GetAsync($"/api/documentos/{id}/arquivo");

        Assert.Equal("nosniff", Assert.Single(resposta.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("attachment", resposta.Content.Headers.ContentDisposition?.DispositionType);
        Assert.False(resposta.Headers.Contains("Accept-Ranges"));
    }

    [Fact]
    public async Task Pdf_falso_retorna_400_e_nada_e_gravado()
    {
        var (ana, alunoId) = await CenarioAsync();
        var antes = ArquivosGravados();

        var resposta = await ana.PostAsync($"/api/alunos/{alunoId}/documentos", Formulario("não é pdf"u8.ToArray(), "falso.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.True((await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("arquivo", out _));
        Assert.Equal(antes, ArquivosGravados());
    }

    [Fact]
    public async Task Arquivo_acima_de_5_mib_retorna_400()
    {
        var (ana, alunoId) = await CenarioAsync();
        var grande = new byte[5 * 1024 * 1024 + 1];
        Pdf.CopyTo(grande, 0);

        var resposta = await ana.PostAsync($"/api/alunos/{alunoId}/documentos", Formulario(grande, "grande.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("O arquivo excede o limite de 5 MiB.", erros.GetProperty("arquivo")[0].GetString());
    }

    [Fact]
    public async Task Corpo_acima_do_teto_do_servidor_retorna_400_no_campo_arquivo()
    {
        var (ana, alunoId) = await CenarioAsync();
        var enorme = new byte[7 * 1024 * 1024];
        Pdf.CopyTo(enorme, 0);

        var resposta = await ana.PostAsync($"/api/alunos/{alunoId}/documentos", Formulario(enorme, "enorme.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("O arquivo excede o limite de 5 MiB.", erros.GetProperty("arquivo")[0].GetString());
    }

    [Fact]
    public async Task Multipart_sem_boundary_retorna_400_em_portugues()
    {
        var (ana, alunoId) = await CenarioAsync();
        var conteudo = new ByteArrayContent(Pdf);
        conteudo.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data");

        var resposta = await ana.PostAsync($"/api/alunos/{alunoId}/documentos", conteudo);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("Formulário inválido.", erros.GetProperty("corpo")[0].GetString());
    }

    [Fact]
    public async Task Coordenador_curso_nao_ve_arquivados_nem_com_incluir_arquivados()
    {
        var (ana, alunoId) = await CenarioAsync();
        var id = await AnexarAsync(ana, alunoId);
        Assert.Equal(HttpStatusCode.NoContent, (await CompartilharAsync(ana, id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await AcaoAsync(ana, HttpMethod.Post, id, "arquivar", new { motivo = "Enviado por engano" })).StatusCode);
        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");

        var lista = await coord.GetFromJsonAsync<JsonElement>($"/api/alunos/{alunoId}/documentos?incluirArquivados=true");
        Assert.Equal(0, lista.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await coord.GetAsync($"/api/documentos/{id}?incluirArquivados=true")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coord.GetAsync($"/api/documentos/{id}/arquivo?incluirArquivados=true")).StatusCode);
    }

    [Fact]
    public async Task Coordenador_curso_anexa_no_proprio_curso_ja_compartilhado_e_404_em_outro_curso()
    {
        var (ana, alunoDireito) = await CenarioAsync();
        var cursos = await ana.GetFromJsonAsync<JsonElement>("/api/cursos");
        var outroCurso = cursos.EnumerateArray().First(c => c.GetProperty("nome").GetString() != "Direito").GetProperty("nome").GetString();
        var (_, alunoOutro) = await CenarioAsync(outroCurso!);
        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");

        var id = await AnexarAsync(coord, alunoDireito);
        var doc = await coord.GetFromJsonAsync<JsonElement>($"/api/documentos/{id}");
        Assert.Equal("CompartilhadoCoordenacao", doc.GetProperty("visibilidade").GetString());
        Assert.Equal(HttpStatusCode.OK, (await ana.GetAsync($"/api/documentos/{id}/arquivo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await coord.PostAsync($"/api/alunos/{alunoOutro}/documentos", Formulario(Pdf, "laudo.pdf"))).StatusCode);
    }

    [Fact]
    public async Task Coordenador_curso_recebe_403_na_escrita()
    {
        var (ana, alunoId) = await CenarioAsync();
        var id = await AnexarAsync(ana, alunoId);
        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/documentos/{id}/arquivar")
        {
            Content = JsonContent.Create(new { motivo = "Tentativa indevida" }),
        };
        req.Headers.Add("If-Match", "\"qualquer\"");
        Assert.Equal(HttpStatusCode.Forbidden, (await coord.SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task Coordenador_baixa_compartilhado_do_proprio_curso()
    {
        var (ana, alunoId) = await CenarioAsync();
        var id = await AnexarAsync(ana, alunoId);
        Assert.Equal(HttpStatusCode.NoContent, (await CompartilharAsync(ana, id)).StatusCode);
        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");

        var lista = await coord.GetFromJsonAsync<JsonElement>($"/api/alunos/{alunoId}/documentos");
        Assert.Equal(id, lista.GetProperty("itens")[0].GetProperty("id").GetGuid());
        var resposta = await coord.GetAsync($"/api/documentos/{id}/arquivo");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(Pdf, await resposta.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Coordenador_recebe_404_em_restrito_e_em_outro_curso()
    {
        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");
        var (ana, alunoDireito) = await CenarioAsync();
        var restrito = await AnexarAsync(ana, alunoDireito);
        var cursos = await ana.GetFromJsonAsync<JsonElement>("/api/cursos");
        var outroCurso = cursos.EnumerateArray().First(c => c.GetProperty("nome").GetString() != "Direito").GetProperty("nome").GetString();
        var (_, alunoOutro) = await CenarioAsync(outroCurso!);
        var deOutroCurso = await AnexarAsync(ana, alunoOutro);
        Assert.Equal(HttpStatusCode.NoContent, (await CompartilharAsync(ana, deOutroCurso)).StatusCode);

        foreach (var id in new[] { restrito, deOutroCurso })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await coord.GetAsync($"/api/documentos/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await coord.GetAsync($"/api/documentos/{id}/arquivo")).StatusCode);
        }
    }

    [Fact]
    public async Task Sip_e_gestao_recebem_403()
    {
        var (ana, alunoId) = await CenarioAsync();
        var id = await AnexarAsync(ana, alunoId);

        foreach (var email in new[] { "sip@nap.demo", "gestao@nap.demo" })
        {
            var cliente = await fabrica.ClienteComo(email);
            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/alunos/{alunoId}/documentos")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/documentos/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/documentos/{id}/arquivo")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await cliente.PostAsync($"/api/alunos/{alunoId}/documentos", Formulario(Pdf, "laudo.pdf"))).StatusCode);
        }
    }

    [Fact]
    public async Task Arquivado_some_da_lista_comum_e_download_404_para_profissional()
    {
        var (ana, alunoId) = await CenarioAsync();
        var id = await AnexarAsync(ana, alunoId);

        Assert.Equal(HttpStatusCode.NoContent,
            (await AcaoAsync(ana, HttpMethod.Post, id, "arquivar", new { motivo = "Enviado por engano" })).StatusCode);

        var lista = await ana.GetFromJsonAsync<JsonElement>($"/api/alunos/{alunoId}/documentos?incluirArquivados=true");
        Assert.Equal(0, lista.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await ana.GetAsync($"/api/documentos/{id}/arquivo?incluirArquivados=true")).StatusCode);

        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        Assert.Equal(0, (await coordenacao.GetFromJsonAsync<JsonElement>($"/api/alunos/{alunoId}/documentos")).GetProperty("total").GetInt32());
        Assert.Equal(1, (await coordenacao.GetFromJsonAsync<JsonElement>($"/api/alunos/{alunoId}/documentos?incluirArquivados=true"))
            .GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await coordenacao.GetAsync($"/api/documentos/{id}/arquivo?incluirArquivados=true")).StatusCode);
    }

    [Fact]
    public async Task Repeticao_com_mesma_chave_nao_duplica()
    {
        var (ana, alunoId) = await CenarioAsync();
        var chave = Guid.NewGuid().ToString();

        async Task<HttpResponseMessage> Postar()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"/api/alunos/{alunoId}/documentos") { Content = Formulario(Pdf, "laudo.pdf") };
            req.Headers.Add("Idempotency-Key", chave);
            return await ana.SendAsync(req);
        }

        var primeira = await Postar();
        var segunda = await Postar();

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);
        var id = (await primeira.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(id, (await segunda.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.EndsWith($"/api/documentos/{id}", segunda.Headers.Location?.ToString());
        Assert.Equal(1, (await ana.GetFromJsonAsync<JsonElement>($"/api/alunos/{alunoId}/documentos")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Categoria_e_revogacao_exigem_if_match()
    {
        var (ana, alunoId) = await CenarioAsync();
        var id = await AnexarAsync(ana, alunoId);

        Assert.Equal((HttpStatusCode)428, (await ana.PutAsJsonAsync($"/api/documentos/{id}/categoria", new { categoria = "Termo" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await AcaoAsync(ana, HttpMethod.Put, id, "categoria", new { categoria = "Termo" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await CompartilharAsync(ana, id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await AcaoAsync(ana, HttpMethod.Post, id, "revogar-compartilhamento", new { justificativa = "Não é mais necessário" })).StatusCode);

        var dto = await ana.GetFromJsonAsync<JsonElement>($"/api/documentos/{id}");
        Assert.Equal("Termo", dto.GetProperty("categoria").GetString());
        Assert.Equal("RestritoNap", dto.GetProperty("visibilidade").GetString());
    }
}
