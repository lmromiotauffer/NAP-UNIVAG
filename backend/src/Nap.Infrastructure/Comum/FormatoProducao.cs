using System.Globalization;
using Nap.Application.Features.Producao;

namespace Nap.Infrastructure.Comum;

/// <summary>Formatos compartilhados pelos arquivos exportados da produção (PDF e Excel).</summary>
internal static class FormatoProducao
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Duracao(int minutos)
    {
        var abs = Math.Abs(minutos);
        return $"{(minutos < 0 ? "-" : "")}{abs / 60}:{abs % 60:00}";
    }

    public static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", PtBr);

    public static string Percentual(decimal? fracao) => fracao is { } f ? f.ToString("P1", PtBr) : "—";

    public static string Variacao(int valor) => valor > 0 ? $"+{valor}" : valor.ToString(PtBr);

    public static string VariacaoDuracao(int minutos) => minutos > 0 ? $"+{Duracao(minutos)}" : Duracao(minutos);

    /// <summary>Ociosidade não tem ocorrências: "—" (o tempo dela aparece normalmente).</summary>
    public static string Ocorrencias(ValorProducaoDto v) => v.Ocorrencias?.ToString(PtBr) ?? "—";

    public static string Taxa(TaxaDto t) =>
        t.Valor is { } v ? $"{Percentual(v)} ({t.Numerador} de {t.Denominador})" : t.Motivo ?? "—";
}
