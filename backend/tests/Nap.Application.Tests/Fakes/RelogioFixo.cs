using Nap.Application.Comum;

namespace Nap.Application.Tests.Fakes;

public sealed class RelogioFixo : IRelogio
{
    public DateTimeOffset Agora { get; init; } = new(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(-3));
    public TimeZoneInfo Fuso { get; } = TimeZoneInfo.Utc;
    public DateOnly Hoje => DateOnly.FromDateTime(Agora.DateTime);
}
