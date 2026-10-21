using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Nap.Application.Comum;
using Nap.Application.Features.Producao;
using static Nap.Infrastructure.Comum.FormatoProducao;

namespace Nap.Infrastructure.Pdf;

public sealed class GeradorPdfProducao : IGeradorPdfProducao
{
    private readonly TimeZoneInfo _fuso;

    public GeradorPdfProducao(IRelogio relogio)
    {
        _fuso = relogio.Fuso;
        ResolvedorFontes.Configurar();
    }

    public byte[] Gerar(ProducaoDto dados, CabecalhoExportacao cabecalho)
    {
        var renderizador = new PdfDocumentRenderer { Document = Montar(dados, cabecalho) };
        renderizador.RenderDocument();
        using var saida = new MemoryStream();
        renderizador.PdfDocument.Save(saida);
        return saida.ToArray();
    }

    /// <summary>Documento antes da renderização; também serve aos testes, já que o texto do PDF final usa glifos indexados.</summary>
    public Document Montar(ProducaoDto dados, CabecalhoExportacao cabecalho)
    {
        var doc = new Document();
        doc.Info.Title = $"Produção do NAP — {cabecalho.Periodo}";
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = ResolvedorFontes.Familia;
        normal.Font.Size = 9;
        normal.ParagraphFormat.SpaceAfter = 3;
        var secao = doc.AddSection();
        secao.PageSetup = doc.DefaultPageSetup.Clone();
        secao.PageSetup.PageFormat = PageFormat.A4;
        secao.PageSetup.LeftMargin = secao.PageSetup.RightMargin = Unit.FromCentimeter(2);

        Titulo(secao, "Produção do NAP", 16);
        secao.AddParagraph($"Período: {cabecalho.Periodo}");
        secao.AddParagraph($"Tipo: {cabecalho.Tipo}");
        secao.AddParagraph($"Filtros: {cabecalho.Filtros}");
        if (dados.Periodo.Parcial) secao.AddParagraph("Período parcial").Format.Font.Bold = true;
        secao.AddParagraph($"Exportado por {cabecalho.ExportadoPor} em {TimeZoneInfo.ConvertTime(cabecalho.Em, _fuso).ToString("dd/MM/yyyy HH:mm", PtBr)}")
            .Format.Font.Italic = true;

        Titulo(secao, "Categorias", 12);
        TabelaCategorias(secao, dados.Categorias);

        Titulo(secao, "Tempo", 12);
        QuadroTempo(secao, dados.Tempo);
        Resumo(secao, dados.TaxaAusencia, dados.Pendentes);

        Titulo(secao, "Série", 12);
        var serie = Tabela(secao, 4, 2.5, 2.5, 2);
        Cabecalho(serie, "Período", "Categoria", "Ocorrências (total)", "Tempo (total)");
        foreach (var s in dados.Serie)
        foreach (var c in s.Categorias)
            Linha(serie, $"{Data(s.Inicio)} a {Data(s.Fim)}{(s.Parcial ? " (parcial)" : "")}", c.Rotulo, Ocorrencias(c.Total), Duracao(c.Total.Minutos));

        Titulo(secao, "Comparação com o período anterior", 12);
        secao.AddParagraph($"Período anterior: {Data(dados.Anterior.Periodo.Inicio)} a {Data(dados.Anterior.Periodo.Fim)}");
        var rotulos = dados.Categorias.ToDictionary(c => c.Chave, c => c.Rotulo);
        var comp = Tabela(secao, 4.4, 1.9, 1.9, 2, 2, 2.4, 2.4);
        Cabecalho(comp, "Categoria", "Ocorr. anterior", "Tempo anterior", "Variação ocorr.", "Variação tempo", "Variação % ocorr.", "Variação % tempo");
        foreach (var v in dados.Anterior.Categorias)
            Linha(comp, rotulos.GetValueOrDefault(v.Chave, v.Chave.ToString()), Ocorrencias(v.Anterior), Duracao(v.Anterior.Minutos),
                v.Diferenca.Ocorrencias is { } d ? Variacao(d) : "—", VariacaoDuracao(v.Diferenca.Minutos),
                Percentual(v.PercentualOcorrencias), Percentual(v.PercentualMinutos));

        if (dados.PorProfissional is { } profissionais)
        {
            Titulo(secao, "Por profissional", 12);
            foreach (var p in profissionais)
            {
                secao.AddParagraph(p.Profissional).Format.Font.Bold = true;
                TabelaCategorias(secao, p.Categorias);
                if (p.Tempo is not null) QuadroTempo(secao, p.Tempo);
                Resumo(secao, p.TaxaAusencia, p.Pendentes);
            }
        }
        return doc;
    }

    private static void QuadroTempo(Section secao, TempoDto? t)
    {
        if (t is null)
        {
            secao.AddParagraph("A visão por curso não tem capacidade.");
            return;
        }
        var q = Tabela(secao, 7, 3);
        Linha(q, "Capacidade", Duracao(t.Capacidade));
        Linha(q, "Produção na escala", Duracao(t.ProducaoNaEscala));
        Linha(q, "Ausências", Duracao(t.AusenciasNaEscala));
        Linha(q, "Pendentes", Duracao(t.PendentesNaEscala));
        Linha(q, "Ociosidade", Duracao(t.Ociosidade));
        Linha(q, "Tempo extra", Duracao(t.TempoExtra));
        if (t.Inconsistente)
            Linha(q, "Inconsistente", string.Join(", ", t.DiasInconsistentes.Select(Data)));
    }

    private static void Resumo(Section secao, TaxaDto taxa, int? pendentes)
    {
        secao.AddParagraph($"Taxa de ausência: {Taxa(taxa)}");
        if (pendentes is { } n) secao.AddParagraph($"Atendimentos pendentes de registro: {n}");
    }

    private static void TabelaCategorias(Section secao, IReadOnlyList<CategoriaProducaoDto> categorias)
    {
        var t = Tabela(secao, 5, 2, 2, 2, 2, 2, 2);
        Cabecalho(t, "Categoria", "Ocorr. registrado", "Ocorr. ajustes", "Ocorr. total", "Tempo registrado", "Tempo ajustes", "Tempo total");
        foreach (var c in categorias)
            Linha(t, c.Rotulo, Ocorrencias(c.Registrado), Ocorrencias(c.Ajustes), Ocorrencias(c.Total),
                Duracao(c.Registrado.Minutos), Duracao(c.Ajustes.Minutos), Duracao(c.Total.Minutos));
    }

    private static Table Tabela(Section secao, params double[] largurasCm)
    {
        var t = secao.AddTable();
        t.Borders.Width = 0.5;
        t.Format.Font.Size = 8;
        foreach (var l in largurasCm) t.AddColumn(Unit.FromCentimeter(l));
        return t;
    }

    private static void Cabecalho(Table t, params string[] titulos)
    {
        var linha = t.AddRow();
        linha.HeadingFormat = true;
        linha.Format.Font.Bold = true;
        for (var i = 0; i < titulos.Length; i++) linha.Cells[i].AddParagraph(titulos[i]);
    }

    private static void Linha(Table t, params string[] valores)
    {
        var linha = t.AddRow();
        for (var i = 0; i < valores.Length; i++) linha.Cells[i].AddParagraph(valores[i]);
    }

    private static void Titulo(Section secao, string texto, int tamanho)
    {
        var p = secao.AddParagraph(texto);
        p.Format.Font.Bold = true;
        p.Format.Font.Size = tamanho;
        p.Format.SpaceBefore = 10;
    }
}
