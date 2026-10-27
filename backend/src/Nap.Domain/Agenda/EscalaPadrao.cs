namespace Nap.Domain.Agenda;

public static class EscalaPadrao
{
    private static readonly TimeOnly Abertura = new(7, 0);
    private static readonly TimeOnly Fechamento = new(17, 0);

    public static IReadOnlyList<FaixaEscala> FaixasEscala { get; } =
    [
        new(DayOfWeek.Monday, Abertura, Fechamento, false),
        new(DayOfWeek.Tuesday, Abertura, Fechamento, false),
        new(DayOfWeek.Wednesday, Abertura, Fechamento, false),
        new(DayOfWeek.Thursday, Abertura, Fechamento, false),
        new(DayOfWeek.Friday, Abertura, Fechamento, true),
    ];
}
