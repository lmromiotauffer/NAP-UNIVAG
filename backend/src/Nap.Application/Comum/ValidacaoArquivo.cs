using System.Globalization;
using System.Text.Unicode;

namespace Nap.Application.Comum;

public sealed record ResultadoArquivo(string Extensao, string TipoConteudo);

/// <summary>Aceita o arquivo só se a extensão for conhecida e o conteúdo corresponder a ela (assinatura binária ou UTF-8).</summary>
public static class ValidacaoArquivo
{
    private const string Campo = "arquivo";

    // Assinatura nula = texto: UTF-8 válido e sem byte nulo.
    private static readonly Dictionary<string, (byte[]? Assinatura, string Tipo)> Formatos = new()
    {
        [".pdf"] = ([0x25, 0x50, 0x44, 0x46, 0x2D], "application/pdf"),
        [".png"] = ([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "image/png"),
        [".jpg"] = ([0xFF, 0xD8, 0xFF], "image/jpeg"),
        [".jpeg"] = ([0xFF, 0xD8, 0xFF], "image/jpeg"),
        [".docx"] = ([0x50, 0x4B, 0x03, 0x04], "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
        [".xlsx"] = ([0x50, 0x4B, 0x03, 0x04], "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        [".txt"] = (null, "text/plain; charset=utf-8"),
        [".csv"] = (null, "text/csv; charset=utf-8"),
    };

    public static ResultadoArquivo Validar(string nomeArquivo, ReadOnlySpan<byte> conteudo, long limiteBytes)
    {
        if (conteudo.IsEmpty)
            throw Validacao.ErroDeCampo(Campo, "Envie um arquivo.");
        if (conteudo.Length > limiteBytes)
            throw ExcedeLimite(limiteBytes);
        var extensao = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        if (!Formatos.TryGetValue(extensao, out var formato))
            throw Validacao.ErroDeCampo(Campo, "Formato não aceito. Envie PDF, PNG, JPG, DOCX, XLSX, TXT ou CSV.");
        var corresponde = formato.Assinatura is { } assinatura
            ? conteudo.StartsWith(assinatura)
            : Utf8.IsValid(conteudo) && !conteudo.Contains((byte)0);
        if (!corresponde)
            throw Validacao.ErroDeCampo(Campo, $"O conteúdo do arquivo não corresponde ao formato {extensao[1..].ToUpperInvariant()}.");
        return new ResultadoArquivo(extensao, formato.Tipo);
    }

    /// <summary>Também usado pela Api quando o corpo passa do teto do servidor.</summary>
    public static ValidationException ExcedeLimite(long limiteBytes)
    {
        var mib = (limiteBytes / 1_048_576d).ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
        return Validacao.ErroDeCampo(Campo, $"O arquivo excede o limite de {mib} MiB.");
    }
}
