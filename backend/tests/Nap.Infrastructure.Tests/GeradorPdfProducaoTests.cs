using System.Text;
using MigraDoc.DocumentObjectModel.IO;
using Nap.Infrastructure.Comum;
using Nap.Infrastructure.Pdf;

namespace Nap.Infrastructure.Tests;

public class GeradorPdfProducaoTests
{
    private static readonly GeradorPdfProducao Gerador = new(new Relogio());

    // O PDF embute fontes com glifos indexados, então o texto é verificado no documento MigraDoc que vira o PDF.
    private static string Texto(GeradorPdfProducao g, Nap.Application.Features.Producao.ProducaoDto d) =>
        DdlWriter.WriteToString(g.Montar(d, ProducaoExportacaoFabrica.Cabecalho));

    /// <summary>Linhas de tabela (cada uma serializada em DDL) cujo texto traz o rótulo; a primeira é a da tabela de categorias.</summary>
    private static IEnumerable<string> LinhasDe(Nap.Application.Features.Producao.ProducaoDto d, string rotulo) =>
        Gerador.Montar(d, ProducaoExportacaoFabrica.Cabecalho).Sections.Cast<MigraDoc.DocumentObjectModel.Section>()
            .SelectMany(sec => sec.Elements.OfType<MigraDoc.DocumentObjectModel.Tables.Table>())
            .SelectMany(t => t.Rows.Cast<MigraDoc.DocumentObjectModel.Tables.Row>())
            .Select(l => DdlWriter.WriteToString(l)).Where(l => l.Contains(rotulo));

    [Fact]
    public void Pdf_comeca_com_assinatura_pdf_e_tem_as_categorias()
    {
        var dto = ProducaoExportacaoFabrica.Dto(porProfissional: true, parcial: true);

        var bytes = Gerador.Gerar(dto, ProducaoExportacaoFabrica.Cabecalho);
        var texto = Texto(Gerador, dto);

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.Contains("+Liberation#20Sans", Encoding.Latin1.GetString(bytes));
        Assert.Contains("Produção do NAP", texto);
        Assert.Contains("01/09/2026 a 30/09/2026", texto);
        Assert.Contains("Período parcial", texto);
        Assert.Contains("Exportado por Ana Lúcia em 03/10/2026", texto);
        Assert.Contains("Atendimentos realizados", texto);
        Assert.Contains("Horários disponíveis sem utilização", texto);
        Assert.Contains("12:05", texto); // 725 min
        Assert.Contains("2:05", texto);  // ajustes 125 min
        Assert.Contains("—", LinhasDe(dto, "Horários disponíveis sem utilização").First()); // ociosidade sem ocorrências
        Assert.Contains("12,5%", texto);
        Assert.Contains("2 de 16", texto);
        Assert.Contains("Inconsistente", texto);
        Assert.Contains("10/09/2026", texto);
        Assert.Contains("Por profissional", texto);
        Assert.Contains("Bruno Prof", texto);
        Assert.Contains("Comparação com o período anterior", texto);
    }

    [Fact]
    public void Pdf_da_visao_de_curso_tem_a_nota_e_nao_tem_capacidade()
    {
        var dto = ProducaoExportacaoFabrica.Dto(curso: true);

        var texto = Texto(Gerador, dto);

        Assert.Contains("A visão por curso não tem capacidade.", texto);
        Assert.DoesNotContain("Capacidade", texto);
        Assert.DoesNotContain("Período parcial", texto);
        Assert.DoesNotContain("Por profissional", texto);
    }
}
