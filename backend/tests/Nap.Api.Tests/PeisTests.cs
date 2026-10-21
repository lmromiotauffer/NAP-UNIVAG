using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nap.Infrastructure.Persistencia;

namespace Nap.Api.Tests;

public class PeisTests(ApiFactory fabrica)
{
    internal const string DemandaInterna = "Demanda interna sigilosa do NAP";

    internal static async Task<Guid> IdAsync(HttpResponseMessage resposta)
    {
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>Ação na versão com o <c>If-Match</c> lido do detalhe (pelo próprio cliente).</summary>
    internal static async Task<HttpResponseMessage> AcaoAsync(HttpClient cliente, Guid versaoId, string acao, object? corpo = null,
        HttpMethod? metodo = null)
    {
        var versao = (await cliente.GetFromJsonAsync<JsonElement>($"/api/peis/versoes/{versaoId}")).GetProperty("versao").GetString();
        var req = new HttpRequestMessage(metodo ?? HttpMethod.Post, $"/api/peis/versoes/{versaoId}/{acao}")
        {
            Content = JsonContent.Create(corpo ?? new { }),
        };
        req.Headers.Add("If-Match", $"\"{versao}\"");
        return await cliente.SendAsync(req);
    }

    internal static async Task Ok(Task<HttpResponseMessage> chamada) => Assert.Equal(HttpStatusCode.NoContent, (await chamada).StatusCode);

    /// <summary>Aluno novo no curso, com PEI criado pela Ana (responsável) e enviado para revisão.</summary>
    internal static async Task<(HttpClient Ana, Guid AlunoId, string Matricula, Guid VersaoId)> EmRevisaoAsync(ApiFactory fabrica,
        string curso = "Direito")
    {
        var ana = await fabrica.ClienteComo("ana.beatriz@nap.demo");
        var anaId = await ApiFactory.IdDoUsuarioAsync(ana);
        var (alunoId, matricula) = await ApiFactory.CriarAlunoAsync(ana, anaId, curso);
        var versaoId = await IdAsync(await ana.PostAsJsonAsync($"/api/alunos/{alunoId}/peis", new
        {
            profissionalResponsavelId = anaId,
            conteudo = new
            {
                demandaInterna = DemandaInterna, necessidadesEducacionais = "Leitura ampliada", adaptacoes = "Tempo adicional",
                vigenciaInicio = "2026-08-01", vigenciaFim = "2027-07-31",
            },
        }));
        await Ok(AcaoAsync(ana, versaoId, "enviar-revisao"));
        return (ana, alunoId, matricula, versaoId);
    }

    /// <summary>Revisão aprovada pela coordenação e assinatura do profissional; devolve o acesso do aluno recém-gerado.</summary>
    internal static async Task<(HttpClient Ana, HttpClient Coordenacao, Guid AlunoId, string Matricula, Guid VersaoId, string Token,
        string Codigo)> AguardandoAlunoAsync(ApiFactory fabrica, string curso = "Direito")
    {
        var (ana, alunoId, matricula, versaoId) = await EmRevisaoAsync(fabrica, curso);
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        await Ok(AcaoAsync(coordenacao, versaoId, "aprovar"));
        await Ok(AcaoAsync(ana, versaoId, "assinar"));
        var acesso = await ana.PostAsync($"/api/peis/versoes/{versaoId}/acesso-aluno", null);
        Assert.Equal(HttpStatusCode.Created, acesso.StatusCode);
        Assert.Equal("no-store", acesso.Headers.CacheControl?.ToString());
        var gerado = await acesso.Content.ReadFromJsonAsync<JsonElement>();
        return (ana, coordenacao, alunoId, matricula, versaoId, gerado.GetProperty("token").GetString()!,
            gerado.GetProperty("codigo").GetString()!);
    }

    internal static async Task<(HttpClient Ana, Guid AlunoId, string Matricula, Guid VersaoId)> PublicadaAsync(ApiFactory fabrica,
        string curso = "Direito")
    {
        var (ana, coordenacao, alunoId, matricula, versaoId, token, codigo) = await AguardandoAlunoAsync(fabrica, curso);
        await Ok(ApiFactory.NovoCliente(fabrica).PostAsJsonAsync("/api/assinar/assinar",
            new { token, codigo, nomeCompleto = "Aluno de Teste", aceite = true }));
        await Ok(AcaoAsync(coordenacao, versaoId, "publicar"));
        return (ana, alunoId, matricula, versaoId);
    }

    private static async Task<JsonElement> EnvioPeiRecebidoAsync(HttpClient coord, Guid alunoId)
    {
        var lista = await coord.GetFromJsonAsync<JsonElement>($"/api/comunicacoes?caixa=Entrada&alunoId={alunoId}");
        return Assert.Single(lista.GetProperty("itens").EnumerateArray(), m => m.GetProperty("tipo").GetString() == "EnvioPei");
    }

    [Fact]
    public async Task Jornada_completa_ate_a_ciencia()
    {
        var (ana, coordenacao, alunoId, _, versaoId, token, codigo) = await AguardandoAlunoAsync(fabrica);
        var aluno = ApiFactory.NovoCliente(fabrica);

        var paraAssinar = await (await aluno.PostAsJsonAsync("/api/assinar/abrir", new { token, codigo }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Aluno de Teste", paraAssinar.GetProperty("aluno").GetString());
        Assert.Equal(JsonValueKind.Null, paraAssinar.GetProperty("conteudo").GetProperty("demandaInterna").ValueKind);
        await Ok(aluno.PostAsJsonAsync("/api/assinar/assinar", new { token, codigo, nomeCompleto = "aluno de teste", aceite = true }));
        await Ok(AcaoAsync(coordenacao, versaoId, "publicar"));

        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");
        var mensagem = await EnvioPeiRecebidoAsync(coord, alunoId);
        Assert.StartsWith("PEI publicado: Aluno de Teste (versão 1)", mensagem.GetProperty("assunto").GetString());
        var detalhe = await coord.GetFromJsonAsync<JsonElement>($"/api/peis/versoes/{versaoId}");
        Assert.Equal("Publicada", detalhe.GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, detalhe.GetProperty("conteudo").GetProperty("demandaInterna").ValueKind);
        Assert.True(detalhe.GetProperty("acoes").GetProperty("podeRegistrarCiencia").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await coord.GetAsync($"/api/peis/versoes/{versaoId}/pdf")).StatusCode);

        await Ok(coord.PostAsync($"/api/peis/versoes/{versaoId}/ciencia", null));
        await Ok(coord.PostAsync($"/api/peis/versoes/{versaoId}/ciencia", null)); // idempotente

        var final = await ana.GetFromJsonAsync<JsonElement>($"/api/peis/versoes/{versaoId}");
        Assert.Equal(DemandaInterna, final.GetProperty("conteudo").GetProperty("demandaInterna").GetString());
        Assert.Equal(2, final.GetProperty("assinaturas").GetArrayLength());
        Assert.Equal(1, final.GetProperty("ciencias").GetArrayLength());
        var lista = await coord.GetFromJsonAsync<JsonElement>($"/api/alunos/{alunoId}/peis");
        Assert.True(lista[0].GetProperty("versoes")[0].GetProperty("cienciaRegistradaPorMim").GetBoolean());
    }

    [Fact]
    public async Task Profissional_recebe_403_ao_publicar()
    {
        var (ana, _, _, versaoId) = await EmRevisaoAsync(fabrica);

        // A política vem antes do If-Match: sem ele, ainda 403.
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.PostAsync($"/api/peis/versoes/{versaoId}/publicar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AcaoAsync(ana, versaoId, "aprovar")).StatusCode);
    }

    [Fact]
    public async Task Coordenador_de_outro_curso_recebe_404()
    {
        var (_, alunoId, _, versaoId) = await PublicadaAsync(fabrica, "Psicologia");
        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");

        Assert.Equal(HttpStatusCode.NotFound, (await coord.GetAsync($"/api/peis/versoes/{versaoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coord.GetAsync($"/api/peis/versoes/{versaoId}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coord.PostAsync($"/api/peis/versoes/{versaoId}/ciencia", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coord.GetAsync($"/api/alunos/{alunoId}/peis")).StatusCode);
    }

    [Fact]
    public async Task Gestao_e_sip_recebem_403()
    {
        var (_, alunoId, _, versaoId) = await EmRevisaoAsync(fabrica);

        foreach (var email in new[] { "gestao@nap.demo", "sip@nap.demo" })
        {
            var cliente = await fabrica.ClienteComo(email);
            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/alunos/{alunoId}/peis")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/peis/versoes/{versaoId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/peis/versoes/{versaoId}/pdf")).StatusCode);
        }
    }

    [Fact]
    public async Task Edicao_sem_if_match_retorna_428()
    {
        var (ana, _, _, versaoId) = await EmRevisaoAsync(fabrica);

        var resposta = await ana.PutAsJsonAsync($"/api/peis/versoes/{versaoId}/conteudo", new
        {
            necessidadesEducacionais = "Outra", vigenciaInicio = "2026-08-01", vigenciaFim = "2027-07-31",
        });

        Assert.Equal((HttpStatusCode)428, resposta.StatusCode);
    }

    [Fact]
    public async Task Pdf_do_coordenador_tem_attachment_e_nome_da_matricula()
    {
        var (_, _, matricula, versaoId) = await PublicadaAsync(fabrica);
        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");

        var resposta = await coord.GetAsync($"/api/peis/versoes/{versaoId}/pdf");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/pdf", resposta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", resposta.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal($"PEI-{matricula.ToUpperInvariant()}-v1.pdf", resposta.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal("nosniff", Assert.Single(resposta.Headers.GetValues("X-Content-Type-Options")));
        Assert.False(resposta.Headers.Contains("Accept-Ranges"));
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(await resposta.Content.ReadAsByteArrayAsync())[..5]);
    }

    [Fact]
    public async Task Coordenador_de_curso_responde_ao_envio_do_pei_e_a_resposta_mantem_a_versao()
    {
        var (_, alunoId, _, versaoId) = await PublicadaAsync(fabrica);
        var coord = await fabrica.ClienteComo("coord.direito@nap.demo");
        var mensagemId = (await EnvioPeiRecebidoAsync(coord, alunoId)).GetProperty("id").GetGuid();

        var respostaId = await IdAsync(await coord.PostAsJsonAsync($"/api/comunicacoes/{mensagemId}/respostas",
            new { corpo = "Ciente, obrigado." }));

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<NapDbContext>();
        var resposta = await db.Mensagens.SingleAsync(m => m.Id == respostaId, TestContext.Current.CancellationToken);
        Assert.Equal(versaoId, resposta.PeiVersaoId);
    }

    [Fact]
    public async Task Nova_versao_e_descarte_do_rascunho()
    {
        var (ana, _, _, versaoId) = await PublicadaAsync(fabrica);
        var peiId = (await ana.GetFromJsonAsync<JsonElement>($"/api/peis/versoes/{versaoId}")).GetProperty("peiId").GetGuid();

        var novaId = await IdAsync(await ana.PostAsync($"/api/peis/{peiId}/versoes", null));
        Assert.Equal(2, (await ana.GetFromJsonAsync<JsonElement>($"/api/peis/versoes/{novaId}")).GetProperty("numero").GetInt32());
        await Ok(AcaoAsync(ana, novaId, "conteudo", new
        {
            necessidadesEducacionais = "Revisada", vigenciaInicio = "2027-08-01", vigenciaFim = "2028-07-31",
        }, HttpMethod.Put));
        var editada = (await ana.GetFromJsonAsync<JsonElement>($"/api/peis/versoes/{novaId}")).GetProperty("conteudo");
        Assert.Equal(("Revisada", "2028-07-31"), (editada.GetProperty("necessidadesEducacionais").GetString(),
            editada.GetProperty("vigenciaFim").GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync($"/api/peis/versoes/{novaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ana.GetAsync($"/api/peis/versoes/{novaId}")).StatusCode);
    }
}
