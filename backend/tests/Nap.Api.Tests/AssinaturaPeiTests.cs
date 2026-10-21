using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nap.Api.Tests;

public class AssinaturaPeiTests(ApiFactory fabrica)
{
    private static string Errado(string codigo) => codigo == "000000" ? "111111" : "000000";

    private static async Task<string> TituloAsync(HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString()!;

    [Fact]
    public async Task Abrir_link_invalido_retorna_404_generico()
    {
        var anonimo = ApiFactory.NovoCliente(fabrica);

        var resposta = await anonimo.PostAsJsonAsync("/api/assinar/abrir", new { token = "token-que-nao-existe", codigo = "123456" });

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("Link inválido ou expirado.", await TituloAsync(resposta));
    }

    [Fact]
    public async Task Cinco_codigos_errados_em_cinco_requisicoes_e_depois_o_certo_da_404()
    {
        var (_, _, _, _, _, token, codigo) = await PeisTests.AguardandoAlunoAsync(fabrica);
        var anonimo = ApiFactory.NovoCliente(fabrica);

        // Cada requisição é um contexto novo: a contagem precisa ter sido gravada no SQL, não só na memória.
        for (var restantes = 4; restantes >= 1; restantes--)
        {
            var errada = await anonimo.PostAsJsonAsync("/api/assinar/abrir", new { token, codigo = Errado(codigo) });
            Assert.Equal(HttpStatusCode.BadRequest, errada.StatusCode);
            var erros = (await errada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            Assert.Equal($"Código incorreto. Restam {restantes} tentativas.", erros.GetProperty("codigo")[0].GetString());
        }
        var quinta = await anonimo.PostAsJsonAsync("/api/assinar/abrir", new { token, codigo = Errado(codigo) });
        Assert.Equal(HttpStatusCode.NotFound, quinta.StatusCode);

        var certa = await anonimo.PostAsJsonAsync("/api/assinar/assinar",
            new { token, codigo, nomeCompleto = "Aluno de Teste", aceite = true });
        Assert.Equal(HttpStatusCode.NotFound, certa.StatusCode);
        Assert.Equal("Link inválido ou expirado.", await TituloAsync(certa));
    }

    [Fact]
    public async Task Rotas_de_assinatura_nao_exigem_autenticacao_e_respondem_no_store()
    {
        var (_, _, _, _, _, token, codigo) = await PeisTests.AguardandoAlunoAsync(fabrica);
        var anonimo = ApiFactory.NovoCliente(fabrica);

        var respostas = new[]
        {
            await anonimo.PostAsJsonAsync("/api/assinar/abrir", new { token, codigo }),
            await anonimo.PostAsJsonAsync("/api/assinar/abrir", new { token = "invalido", codigo }),
            await anonimo.PostAsJsonAsync("/api/assinar/abrir", new { token }),
            await anonimo.PostAsync("/api/assinar/abrir", new StringContent("{", System.Text.Encoding.UTF8, "application/json")),
            await anonimo.PostAsJsonAsync("/api/assinar/assinar", new { token, codigo, nomeCompleto = "Aluno de Teste", aceite = true }),
        };

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest,
            HttpStatusCode.NoContent], respostas.Select(r => r.StatusCode));
        foreach (var r in respostas)
        {
            Assert.Equal("no-store", r.Headers.CacheControl?.ToString());
            Assert.Equal("no-referrer", Assert.Single(r.Headers.GetValues("Referrer-Policy")));
            Assert.False(r.Headers.Contains("Set-Cookie"));
        }
    }
}
