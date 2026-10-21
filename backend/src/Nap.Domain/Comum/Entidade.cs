namespace Nap.Domain.Comum;

public abstract class Entidade
{
    protected Entidade() => Id = Guid.CreateVersion7();

    public Guid Id { get; protected set; }
}
