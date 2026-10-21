using Nap.Domain.Comum;

namespace Nap.Domain.Agenda;

/// <summary>Histórico de uma correção do horário efetivo de um compromisso realizado.</summary>
public class CorrecaoRealizacao : Entidade
{
    public Guid AutorId { get; private set; }
    public DateTimeOffset Em { get; private set; }
    public string Justificativa { get; private set; } = null!;
    public DateTimeOffset InicioAnterior { get; private set; }
    public DateTimeOffset FimAnterior { get; private set; }

    private CorrecaoRealizacao() { } // ORM

    internal static CorrecaoRealizacao Criar(Guid autorId, DateTimeOffset em, string justificativa,
        DateTimeOffset inicioAnterior, DateTimeOffset fimAnterior) =>
        new() { AutorId = autorId, Em = em, Justificativa = justificativa, InicioAnterior = inicioAnterior, FimAnterior = fimAnterior };
}
