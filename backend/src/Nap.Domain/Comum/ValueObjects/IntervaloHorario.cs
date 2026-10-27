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

    /// <summary>Segundos e frações são descartados: produção, painel e listas contam minutos inteiros e só assim concordam.</summary>
    public static IntervaloHorario Criar(DateTimeOffset inicio, DateTimeOffset fim)
    {
        var i = Minuto(inicio);
        var f = Minuto(fim);
        if (f <= i)
            throw new DomainException("O horário de término deve ser posterior ao de início.");
        if (f - i > TimeSpan.FromHours(12))
            throw new DomainException("A duração não pode ultrapassar 12 horas.");
        return new IntervaloHorario(i, f);
    }

    /// <summary>Instante em UTC truncado ao minuto: a unidade com que o intervalo é gravado e contado.</summary>
    public static DateTimeOffset Minuto(DateTimeOffset t)
    {
        t = t.ToUniversalTime();
        return t.AddTicks(-(t.Ticks % TimeSpan.TicksPerMinute));
    }
}
