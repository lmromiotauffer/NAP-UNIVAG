using PdfSharp.Fonts;

namespace Nap.Infrastructure.Pdf;

/// <summary>
/// Toda família pedida vira Liberation Sans embarcada (OFL 1.1): nenhuma fonte do sistema entra, então o PDF sai igual
/// em Windows, CI e Linux.
/// </summary>
public sealed class ResolvedorFontes : IFontResolver
{
    public const string Familia = "Liberation Sans";
    private const string Regular = "LiberationSans-Regular";
    private const string Negrito = "LiberationSans-Bold";

    private static readonly ResolvedorFontes Instancia = new();
    private static readonly Lock Trava = new();
    private static readonly Lazy<byte[]> BytesRegular = new(() => Ler(Regular));
    private static readonly Lazy<byte[]> BytesNegrito = new(() => Ler(Negrito));

    private ResolvedorFontes() { }

    /// <summary>Idempotente: o PDFsharp exige sempre a mesma instância em cada atribuição.</summary>
    public static void Configurar()
    {
        lock (Trava)
        {
            if (!ReferenceEquals(GlobalFontSettings.FontResolver, Instancia)) GlobalFontSettings.FontResolver = Instancia;
        }
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) =>
        new(bold ? Negrito : Regular, mustSimulateBold: false, mustSimulateItalic: italic);

    public byte[]? GetFont(string faceName) => faceName == Negrito ? BytesNegrito.Value : BytesRegular.Value;

    private static byte[] Ler(string nome)
    {
        using var recurso = typeof(ResolvedorFontes).Assembly.GetManifestResourceStream($"Nap.Infrastructure.Pdf.Fontes.{nome}.ttf")
            ?? throw new InvalidOperationException($"Fonte {nome} não embarcada.");
        using var memoria = new MemoryStream();
        recurso.CopyTo(memoria);
        return memoria.ToArray();
    }
}
