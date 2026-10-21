namespace Nap.Domain.Agenda;

public static class EscalaPadrao
{
    private static readonly TimeOnly Abertura = new(7, 0);
    private static readonly TimeOnly Fechamento = new(17, 0);

    public static IReadOnlyList<(DayOfWeek Dia, TimeOnly Inicio, TimeOnly Fim, bool SemAtendimento)> Faixas { get; } =
    [
        (DayOfWeek.Monday, Abertura, Fechamento, false),
        (DayOfWeek.Tuesday, Abertura, Fechamento, false),
        (DayOfWeek.Wednesday, Abertura, Fechamento, false),
        (DayOfWeek.Thursday, Abertura, Fechamento, false),
        (DayOfWeek.Friday, Abertura, Fechamento, true),
    ];

    // Mesmas faixas como VO, para usar onde se espera as de uma EscalaProfissional.
    public static IReadOnlyList<FaixaEscala> FaixasEscala { get; } =
        Faixas.Select(f => new FaixaEscala(f.Dia, f.Inicio, f.Fim, f.SemAtendimento)).ToList();
}
