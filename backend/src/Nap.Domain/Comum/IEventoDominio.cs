namespace Nap.Domain.Comum;

public interface IEventoDominio
{
    Guid EventoId { get; }
    DateTimeOffset OcorridoEm { get; }
}
