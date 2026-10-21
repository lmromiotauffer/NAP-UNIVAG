using Nap.Application.Comum;

namespace Nap.Infrastructure.Comum;

public sealed class Relogio : IRelogio
{
    /// <summary>Fuso institucional (id IANA; o .NET aceita IANA também no Windows).</summary>
    public const string FusoInstitucional = "America/Cuiaba";

    public DateTimeOffset Agora => DateTimeOffset.UtcNow;
    public TimeZoneInfo Fuso { get; } = TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional);
    public DateOnly Hoje => Fuso.DataLocal(Agora);
}
