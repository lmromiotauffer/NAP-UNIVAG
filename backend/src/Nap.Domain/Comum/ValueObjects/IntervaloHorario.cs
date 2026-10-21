namespace Nap.Domain.Comum.ValueObjects;

public sealed record IntervaloHorario
{
    public DateTimeOffset Inicio { get; }
    public DateTimeOffset Fim { get; }

    private IntervaloHorario(DateTimeOffset inicio, DateTimeOffset fim)
    {
        Inicio = inicio;
        Fim = fim;
    }

    public static IntervaloHorario Criar(DateTimeOffset inicio, DateTimeOffset fim)
    {
        var i = inicio.ToUniversalTime();
        var f = fim.ToUniversalTime();
        if (f <= i)
            throw new DomainException("O horário de término deve ser posterior ao de início.");
        if (f - i > TimeSpan.FromHours(12))
            throw new DomainException("A duração não pode ultrapassar 12 horas.");
        return new IntervaloHorario(i, f);
    }
}
