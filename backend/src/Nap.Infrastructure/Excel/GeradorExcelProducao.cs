using ClosedXML.Excel;
using Nap.Application.Comum;
using Nap.Application.Features.Producao;
using static Nap.Infrastructure.Comum.FormatoProducao;

namespace Nap.Infrastructure.Excel;

public sealed class GeradorExcelProducao : IGeradorExcelProducao
{
    // Código invariante: o Excel em pt-BR exibe como 0,0%.
    private const string FormatoPercentual = "0.0%";
    private readonly TimeZoneInfo _fuso;

    public GeradorExcelProducao(IRelogio relogio) => _fuso = relogio.Fuso;

    public byte[] Gerar(ProducaoDto dados, CabecalhoExportacao cabecalho)
    {
        using var livro = new XLWorkbook();
        Resumo(Folha(livro, "Resumo", "Campo", "Valor", "Minutos"), dados, cabecalho);

        var cat = Folha(livro, "Categorias", "Categoria", "Ocorrências (registrado)", "Ocorrências (ajustes)", "Ocorrências (total)",
            "Registrado (minutos)", "Registrado (h:mm)", "Ajustes (minutos)", "Ajustes (h:mm)", "Total (minutos)", "Total (h:mm)");
        var l = 2;
        foreach (var c in dados.Categorias)
        {
            cat.Cell(l, 1).Value = c.Rotulo;
            Ocorrencias(cat.Cell(l, 2), c.Registrado);
            Ocorrencias(cat.Cell(l, 3), c.Ajustes);
            Ocorrencias(cat.Cell(l, 4), c.Total);
            Tempos(cat, l, 5, c);
            l++;
        }

        var serie = Folha(livro, "Serie", "Início", "Fim", "Parcial", "Categoria", "Ocorrências (total)", "Minutos (total)", "Total (h:mm)");
        l = 2;
        foreach (var s in dados.Serie)
        foreach (var c in s.Categorias)
        {
            serie.Cell(l, 1).Value = s.Inicio.ToDateTime(TimeOnly.MinValue);
            serie.Cell(l, 2).Value = s.Fim.ToDateTime(TimeOnly.MinValue);
            serie.Range(l, 1, l, 2).Style.DateFormat.Format = "dd/MM/yyyy";
            serie.Cell(l, 3).Value = s.Parcial ? "Sim" : "Não";
            serie.Cell(l, 4).Value = c.Rotulo;
            Ocorrencias(serie.Cell(l, 5), c.Total);
            serie.Cell(l, 6).Value = c.Total.Minutos;
            serie.Cell(l, 7).Value = Duracao(c.Total.Minutos);
            l++;
        }

        var comp = Folha(livro, "Comparacao", "Categoria", "Anterior (ocorrências)", "Anterior (minutos)", "Anterior (h:mm)", "Variação (ocorrências)",
            "Variação (minutos)", "Variação (% ocorrências)", "Variação (% minutos)");
        var rotulos = dados.Categorias.ToDictionary(c => c.Chave, c => c.Rotulo);
        l = 2;
        foreach (var v in dados.Anterior.Categorias)
        {
            comp.Cell(l, 1).Value = rotulos.GetValueOrDefault(v.Chave, v.Chave.ToString());
            Ocorrencias(comp.Cell(l, 2), v.Anterior);
            comp.Cell(l, 3).Value = v.Anterior.Minutos;
            comp.Cell(l, 4).Value = Duracao(v.Anterior.Minutos);
            if (v.Diferenca.Ocorrencias is { } d) comp.Cell(l, 5).Value = d; else comp.Cell(l, 5).Value = "—";
            comp.Cell(l, 6).Value = v.Diferenca.Minutos;
            Percentual(comp.Cell(l, 7), v.PercentualOcorrencias);
            Percentual(comp.Cell(l, 8), v.PercentualMinutos);
            l++;
        }

        if (dados.PorProfissional is { } profissionais)
        {
            var pp = Folha(livro, "PorProfissional", "Profissional", "Categoria", "Ocorrências (registrado)", "Ocorrências (ajustes)",
                "Ocorrências (total)", "Registrado (minutos)", "Registrado (h:mm)", "Ajustes (minutos)", "Ajustes (h:mm)", "Total (minutos)",
                "Total (h:mm)", "Taxa de ausência", "Pendentes");
            l = 2;
            foreach (var p in profissionais)
            {
                var primeira = true;
                foreach (var c in p.Categorias)
                {
                    pp.Cell(l, 1).Value = p.Profissional;
                    pp.Cell(l, 2).Value = c.Rotulo;
                    Ocorrencias(pp.Cell(l, 3), c.Registrado);
                    Ocorrencias(pp.Cell(l, 4), c.Ajustes);
                    Ocorrencias(pp.Cell(l, 5), c.Total);
                    Tempos(pp, l, 6, c);
                    if (primeira)
                    {
                        if (p.TaxaAusencia.Valor is null) pp.Cell(l, 12).Value = p.TaxaAusencia.Motivo ?? "—";
                        else Percentual(pp.Cell(l, 12), p.TaxaAusencia.Valor);
                        if (p.Pendentes is { } n) pp.Cell(l, 13).Value = n;
                        primeira = false;
                    }
                    l++;
                }
            }
        }

        foreach (var f in livro.Worksheets) f.Columns().AdjustToContents();
        using var saida = new MemoryStream();
        livro.SaveAs(saida);
        return saida.ToArray();
    }

    private void Resumo(IXLWorksheet f, ProducaoDto d, CabecalhoExportacao c)
    {
        var l = 2;
        void Texto(string campo, string valor) { f.Cell(l, 1).Value = campo; f.Cell(l, 2).Value = valor; l++; }
        void Minutos(string campo, int minutos) { f.Cell(l, 1).Value = campo; f.Cell(l, 2).Value = Duracao(minutos); f.Cell(l, 3).Value = minutos; l++; }

        Texto("Produção do NAP", "");
        Texto("Período", c.Periodo);
        Texto("Tipo", c.Tipo);
        Texto("Filtros", c.Filtros);
        if (d.Periodo.Parcial) Texto("Período parcial", "Sim");
        Texto("Exportado por", $"{c.ExportadoPor} em {TimeZoneInfo.ConvertTime(c.Em, _fuso).ToString("dd/MM/yyyy HH:mm", PtBr)}");
        if (d.Tempo is { } t)
        {
            Minutos("Capacidade", t.Capacidade);
            Minutos("Produção na escala", t.ProducaoNaEscala);
            Minutos("Ausências", t.AusenciasNaEscala);
            Minutos("Pendentes", t.PendentesNaEscala);
            Minutos("Ociosidade", t.Ociosidade);
            Minutos("Tempo extra", t.TempoExtra);
            if (t.Inconsistente) Texto("Inconsistente", string.Join(", ", t.DiasInconsistentes.Select(Data)));
        }
        else Texto("Capacidade", "A visão por curso não tem capacidade.");

        f.Cell(l, 1).Value = "Taxa de ausência";
        if (d.TaxaAusencia.Valor is { } v)
        {
            Percentual(f.Cell(l, 2), v);
            l++;
            Texto("Base da taxa de ausência", $"{d.TaxaAusencia.Numerador} de {d.TaxaAusencia.Denominador}");
        }
        else
        {
            f.Cell(l, 2).Value = d.TaxaAusencia.Motivo ?? "—";
            l++;
        }
        if (d.Pendentes is { } n) { f.Cell(l, 1).Value = "Atendimentos pendentes de registro"; f.Cell(l, 2).Value = n; }
    }

    /// <summary>Registrado, ajustes e total: minutos como número e a coluna formatada ao lado.</summary>
    private static void Tempos(IXLWorksheet f, int l, int coluna, CategoriaProducaoDto c)
    {
        foreach (var v in new[] { c.Registrado, c.Ajustes, c.Total })
        {
            f.Cell(l, coluna++).Value = v.Minutos;
            f.Cell(l, coluna++).Value = Duracao(v.Minutos);
        }
    }

    private static void Ocorrencias(IXLCell cel, ValorProducaoDto v)
    {
        if (v.Ocorrencias is { } o) cel.Value = o; else cel.Value = "—";
    }

    private static void Percentual(IXLCell cel, decimal? fracao)
    {
        if (fracao is { } f)
        {
            cel.Value = f;
            cel.Style.NumberFormat.Format = FormatoPercentual;
        }
        else cel.Value = "—";
    }

    private static IXLWorksheet Folha(XLWorkbook livro, string nome, params string[] titulos)
    {
        var f = livro.AddWorksheet(nome);
        for (var i = 0; i < titulos.Length; i++) f.Cell(1, i + 1).Value = titulos[i];
        f.Row(1).Style.Font.Bold = true;
        f.SheetView.FreezeRows(1);
        return f;
    }
}
