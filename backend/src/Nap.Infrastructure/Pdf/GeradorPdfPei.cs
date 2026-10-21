using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Nap.Application.Comum;
using Nap.Application.Features.Peis;
using Nap.Domain.Peis;

namespace Nap.Infrastructure.Pdf;

public sealed class GeradorPdfPei : IGeradorPdfPei
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly TimeZoneInfo _fuso;

    public GeradorPdfPei(IRelogio relogio)
    {
        _fuso = relogio.Fuso;
        // Também configurado na inicialização; aqui garante o resolvedor antes da primeira fonte quando usado direto.
        ResolvedorFontes.Configurar();
    }

    public byte[] Gerar(ModeloPdfPei m)
    {
        var doc = new Document();
        doc.Info.Title = $"PEI — {m.Aluno} (versão {m.Numero})";
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = ResolvedorFontes.Familia;
        normal.Font.Size = 10;
        normal.ParagraphFormat.SpaceAfter = 4;
        var secao = doc.AddSection();
        secao.PageSetup = doc.DefaultPageSetup.Clone();
        secao.PageSetup.PageFormat = PageFormat.A4;
        secao.PageSetup.LeftMargin = secao.PageSetup.RightMargin = Unit.FromCentimeter(2);

        Titulo(secao, "Plano Educacional Individualizado (PEI)", 16);
        var tabela = secao.AddTable();
        tabela.Borders.Width = 0.5;
        tabela.AddColumn(Unit.FromCentimeter(4));
        tabela.AddColumn(Unit.FromCentimeter(13));
        foreach (var (rotulo, valor) in new[]
        {
            ("Aluno", m.Aluno), ("Matrícula", m.Matricula), ("Curso", m.Curso), ("Responsável", m.Responsavel),
            ("Versão", m.Numero.ToString(PtBr)), ("Estado", m.Estado),
            ("Vigência", $"{Data(m.VigenciaInicio)} a {Data(m.VigenciaFim)}"),
        })
        {
            var linha = tabela.AddRow();
            linha.Cells[0].AddParagraph(rotulo).Format.Font.Bold = true;
            linha.Cells[1].AddParagraph(valor);
        }

        foreach (var (titulo, texto) in m.Secoes)
        {
            Titulo(secao, titulo, 12);
            secao.AddParagraph(texto);
        }

        Titulo(secao, "Assinaturas", 12);
        if (m.Assinaturas.Count == 0) secao.AddParagraph("Nenhuma assinatura registrada.");
        foreach (var a in m.Assinaturas)
        {
            secao.AddParagraph($"{a.Nome} — {Papel(a.Papel)}").Format.Font.Bold = true;
            secao.AddParagraph($"Em {Instante(a.Em)} · método: {a.Metodo}");
            secao.AddParagraph(m.AvisoAssinatura).Format.Font.Italic = true;
        }
        if (m.HashConteudo is not null)
            secao.AddParagraph($"Hash do conteúdo (SHA-256): {Hash(m.HashConteudo)}").Format.Font.Size = 8;

        Titulo(secao, "Publicação", 12);
        secao.AddParagraph(m.PublicadaEm is { } em ? $"Publicada em {Instante(em)}." : "Versão não publicada.");

        Titulo(secao, "Ciência da coordenação do curso", 12);
        if (m.Ciencias.Count == 0) secao.AddParagraph("Nenhuma ciência registrada.");
        foreach (var c in m.Ciencias) secao.AddParagraph($"{c.Coordenador} — {Instante(c.Em)}");

        var renderizador = new PdfDocumentRenderer { Document = doc };
        renderizador.RenderDocument();
        using var saida = new MemoryStream();
        renderizador.PdfDocument.Save(saida);
        return saida.ToArray();
    }

    private static void Titulo(Section secao, string texto, int tamanho)
    {
        var p = secao.AddParagraph(texto);
        p.Format.Font.Bold = true;
        p.Format.Font.Size = tamanho;
        p.Format.SpaceBefore = 10;
    }

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", PtBr);

    private string Instante(DateTimeOffset t) => TimeZoneInfo.ConvertTime(t, _fuso).ToString("dd/MM/yyyy HH:mm", PtBr);

    private static string Papel(PapelAssinatura p) => p switch
    {
        PapelAssinatura.Profissional => "Profissional responsável",
        PapelAssinatura.Aluno => "Aluno",
        _ => p.ToString(),
    };

    // O hex de 64 caracteres não tem ponto de quebra: em grupos de 8 cabe na margem.
    private static string Hash(string h) => string.Join(' ', h.Chunk(8).Select(c => new string(c)));
}
