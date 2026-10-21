using ClosedXML.Excel;
using Nap.Infrastructure.Excel;

namespace Nap.Infrastructure.Tests;

public class GeradorExcelProducaoTests
{
    private static readonly GeradorExcelProducao Gerador = new(new Nap.Infrastructure.Comum.Relogio());

    private static XLWorkbook Abrir(byte[] bytes) => new(new MemoryStream(bytes));

    [Fact]
    public void Excel_tem_as_abas_e_minutos_numericos()
    {
        var bytes = Gerador.Gerar(ProducaoExportacaoFabrica.Dto(porProfissional: true), ProducaoExportacaoFabrica.Cabecalho);

        using var livro = Abrir(bytes);
        Assert.Equal(["Resumo", "Categorias", "Serie", "Comparacao", "PorProfissional"], livro.Worksheets.Select(w => w.Name));
        var cat = livro.Worksheet("Categorias");
        var colunaMinutos = cat.Row(1).CellsUsed().Where(c => c.GetString() == "Total (minutos)").Single().Address.ColumnNumber;
        var celula = cat.Cell(2, colunaMinutos);
        Assert.Equal(XLDataType.Number, celula.DataType);
        Assert.Equal(725, celula.GetDouble());
        Assert.Contains(cat.Row(1).CellsUsed(), c => c.GetString() == "Total (h:mm)");
        Assert.Equal(1, cat.SheetView.SplitRow);
    }

    [Fact]
    public void Excel_sem_por_profissional_nao_cria_a_aba()
    {
        var bytes = Gerador.Gerar(ProducaoExportacaoFabrica.Dto(), ProducaoExportacaoFabrica.Cabecalho);

        using var livro = Abrir(bytes);
        Assert.False(livro.Worksheets.TryGetWorksheet("PorProfissional", out _));
        Assert.Equal(4, livro.Worksheets.Count);
    }

    [Fact]
    public void Excel_percentual_com_formato_de_porcentagem()
    {
        var bytes = Gerador.Gerar(ProducaoExportacaoFabrica.Dto(), ProducaoExportacaoFabrica.Cabecalho);

        using var livro = Abrir(bytes);
        var comp = livro.Worksheet("Comparacao");
        var coluna = comp.Row(1).CellsUsed().Single(c => c.GetString() == "Variação (% minutos)").Address.ColumnNumber;
        var celula = comp.Cell(2, coluna);
        Assert.Equal(XLDataType.Number, celula.DataType);
        Assert.Equal(0.45, celula.GetDouble(), 6);
        Assert.Equal("0.0%", celula.Style.NumberFormat.Format);
        // sem base: vazio/"—", nunca número
        Assert.NotEqual(XLDataType.Number, comp.Cell(3, coluna).DataType);
    }

    [Fact]
    public void Excel_resumo_mantem_a_coluna_minutos_so_numerica_e_poe_a_base_da_taxa_no_texto()
    {
        var bytes = Gerador.Gerar(ProducaoExportacaoFabrica.Dto(), ProducaoExportacaoFabrica.Cabecalho);

        using var livro = Abrir(bytes);
        var resumo = livro.Worksheet("Resumo");
        var linhas = resumo.RowsUsed().Skip(1).ToList();
        Assert.All(linhas.Select(l => l.Cell(3)).Where(c => !c.IsEmpty()), c => Assert.Equal(XLDataType.Number, c.DataType));
        var base_ = linhas.Single(l => l.Cell(1).GetString() == "Base da taxa de ausência");
        Assert.Equal("2 de 16", base_.Cell(2).GetString());
    }

    [Fact]
    public void Excel_por_profissional_sem_taxa_escreve_o_motivo()
    {
        var bytes = Gerador.Gerar(ProducaoExportacaoFabrica.Dto(porProfissional: true, taxaSemValor: true), ProducaoExportacaoFabrica.Cabecalho);

        using var livro = Abrir(bytes);
        var pp = livro.Worksheet("PorProfissional");
        var coluna = pp.Row(1).CellsUsed().Single(c => c.GetString() == "Taxa de ausência").Address.ColumnNumber;
        Assert.Equal("Sem atendimentos no período.", pp.Cell(2, coluna).GetString());
    }
}
