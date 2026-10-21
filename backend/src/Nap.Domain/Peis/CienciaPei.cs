using Nap.Domain.Comum;

namespace Nap.Domain.Peis;

public class CienciaPei : Entidade
{
    public Guid CoordenadorId { get; private set; }
    public DateTimeOffset Em { get; private set; }

    private CienciaPei() { } // ORM

    internal static CienciaPei Criar(Guid coordenadorId, DateTimeOffset em) => new() { CoordenadorId = coordenadorId, Em = em };
}
