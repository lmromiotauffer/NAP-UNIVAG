using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nap.Infrastructure.Persistencia;

namespace Nap.Api.Tests;

public class ExportarProducaoTests(ApiFactory fabrica)
{
    private const string Url = "/api/producao/exportar?tipo=Mes&referencia=2026-09-15";

    [Fact]
    public async Task Pdf_baixa_com_content_disposition_e_no_store()
    {
        var cliente = await fabrica.ClienteComo("coordenacao@nap.demo");

        var resposta = await cliente.GetAsync($"{Url}&formato=pdf", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/pdf", resposta.Content.Headers.ContentType?.MediaType);
        var disposicao = resposta.Content.Headers.ContentDisposition!;
        Assert.Equal("attachment", disposicao.DispositionType);
        Assert.Equal("Producao-mes-2026-09-01.pdf", disposicao.FileNameStar);
        Assert.Equal("no-store", resposta.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", resposta.Headers.GetValues("X-Content-Type-Options").Single());
        var bytes = await resposta.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<NapDbContext>();
        Assert.True(await db.EventosAuditoria.AnyAsync(e => e.Acao == "producao.exportar.pdf" && e.Recurso == "Producao" && e.RecursoId == null,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Xlsx_baixa_com_o_tipo_certo()
    {
        var cliente = await fabrica.ClienteComo("ana.beatriz@nap.demo");

        var resposta = await cliente.GetAsync($"{Url}&formato=xlsx", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", resposta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Producao-mes-2026-09-01.xlsx", resposta.Content.Headers.ContentDisposition!.FileNameStar);
        var bytes = await resposta.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal("PK", Encoding.ASCII.GetString(bytes, 0, 2));
    }

    [Fact]
    public async Task Formato_invalido_retorna_400_em_formato()
    {
        var cliente = await fabrica.ClienteComo("coordenacao@nap.demo");

        var resposta = await cliente.GetAsync($"{Url}&formato=docx", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("\"formato\"", await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Sip_recebe_403_na_exportacao()
    {
        var sip = await fabrica.ClienteComo("sip@nap.demo");

        Assert.Equal(HttpStatusCode.Forbidden, (await sip.GetAsync($"{Url}&formato=pdf", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Coordenador_de_curso_exporta_sem_capacidade()
    {
        var coordenador = await fabrica.ClienteComo("coord.direito@nap.demo");

        var resposta = await coordenador.GetAsync($"{Url}&formato=xlsx", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var livro = new ClosedXML.Excel.XLWorkbook(await resposta.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var textos = livro.Worksheet("Resumo").CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("A visão por curso não tem capacidade.", textos);
        Assert.DoesNotContain("Produção na escala", textos);
    }
}
