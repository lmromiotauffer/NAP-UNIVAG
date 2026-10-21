namespace Nap.Application.Comum;

public interface IRelogio
{
    DateTimeOffset Agora { get; }
    TimeZoneInfo Fuso { get; }
    DateOnly Hoje { get; }
}

public static class FusoExtensoes
{
    /// <summary>Instante UTC da meia-noite local da data no fuso informado.</summary>
    public static DateTimeOffset InicioDoDia(this TimeZoneInfo fuso, DateOnly dia) => fuso.Instante(dia, TimeOnly.MinValue);

    /// <summary>Instante UTC da data e hora locais no fuso informado.</summary>
    public static DateTimeOffset Instante(this TimeZoneInfo fuso, DateOnly data, TimeOnly hora)
    {
        // Hora pulada pelo horário de verão (fusos que já o tiveram): vale o primeiro horário válido depois dela.
        var local = data.ToDateTime(hora, DateTimeKind.Unspecified);
        while (fuso.IsInvalidTime(local)) local = local.AddMinutes(15);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, fuso), TimeSpan.Zero);
    }

    /// <summary>Data civil do instante no fuso informado.</summary>
    public static DateOnly DataLocal(this TimeZoneInfo fuso, DateTimeOffset instante) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, fuso).DateTime);
}
