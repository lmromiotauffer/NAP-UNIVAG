using Nap.Domain.Comum;

namespace Nap.Domain.Producao;

public enum TipoEventoFechamento { Fechamento, Reabertura }

public class EventoFechamento : Entidade
{
    public TipoEventoFechamento Tipo { get; private set; }
    public Guid AutorId { get; private set; }
    public DateTimeOffset Em { get; private set; }
    public string? Justificativa { get; private set; }

    private EventoFechamento() { } // ORM

    internal static EventoFechamento Criar(TipoEventoFechamento tipo, Guid autorId, DateTimeOffset em, string? justificativa) =>
        new() { Tipo = tipo, AutorId = autorId, Em = em, Justificativa = justificativa };
}
