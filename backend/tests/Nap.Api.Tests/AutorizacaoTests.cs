using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nap.Api.Tests;

public class AutorizacaoTests(ApiFactory fabrica)
{
    [Fact]
    public async Task Sem_sessao_recebe_401_problem_details()
    {
        var resposta = await ApiFactory.NovoCliente(fabrica).GetAsync("/api/alunos");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Gestao_recebe_403_em_alunos()
    {
        var cliente = await fabrica.ClienteComo("gestao@nap.demo");

        var resposta = await cliente.GetAsync("/api/alunos");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Coordenador_de_curso_recebe_403_em_atendimentos()
    {
        var cliente = await fabrica.ClienteComo("coord.direito@nap.demo");

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/atendimentos")).StatusCode);
    }

    [Fact]
    public async Task Coordenador_de_curso_recebe_404_ao_trocar_id_de_aluno_de_outro_curso()
    {
        var coordenacao = await fabrica.ClienteComo("coordenacao@nap.demo");
        var todos = await coordenacao.GetFromJsonAsync<JsonElement>("/api/alunos?tamanho=100");
        var deOutroCurso = todos.GetProperty("itens").EnumerateArray().FirstOrDefault(a => a.GetProperty("curso").GetString() != "Direito");
        Assert.True(deOutroCurso.ValueKind == JsonValueKind.Object, "O seed deveria ter alunos de outros cursos além de Direito.");
        var coordenador = await fabrica.ClienteComo("coord.direito@nap.demo");

        var resposta = await coordenador.GetAsync($"/api/alunos/{deOutroCurso.GetProperty("id").GetGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Coordenador_de_curso_nao_recebe_campos_clinicos()
    {
        var coordenador = await fabrica.ClienteComo("coord.direito@nap.demo");

        var lista = await coordenador.GetFromJsonAsync<JsonElement>("/api/alunos");
        var itens = lista.GetProperty("itens").EnumerateArray().ToList();
        Assert.NotEmpty(itens);
        Assert.All(itens, a =>
        {
            Assert.Equal("Direito", a.GetProperty("curso").GetString());
            Assert.Equal(JsonValueKind.Null, a.GetProperty("totalAtendimentos").ValueKind);
        });

        var ficha = await coordenador.GetFromJsonAsync<JsonElement>($"/api/alunos/{itens[0].GetProperty("id").GetGuid()}");
        Assert.True(!ficha.TryGetProperty("resumo", out var resumo) || resumo.ValueKind == JsonValueKind.Null);
    }
}
