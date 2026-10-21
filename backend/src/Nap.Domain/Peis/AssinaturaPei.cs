using Nap.Domain.Comum;

namespace Nap.Domain.Peis;

public class AssinaturaPei : Entidade
{
    public PapelAssinatura Papel { get; private set; }
    public Guid? SignatarioId { get; private set; }
    public string NomeSignatario { get; private set; } = null!;
    public DateTimeOffset Em { get; private set; }
    public string Metodo { get; private set; } = null!;
    public string HashConteudo { get; private set; } = null!;
    public string Evidencia { get; private set; } = null!;

    private AssinaturaPei() { } // ORM

    internal static AssinaturaPei Criar(PapelAssinatura papel, Guid? signatarioId, string nome, string hashConteudo,
        string evidencia, DateTimeOffset em) =>
        new()
        {
            Papel = papel, SignatarioId = signatarioId, NomeSignatario = nome, Em = em, Metodo = "Demonstrativa",
            HashConteudo = hashConteudo, Evidencia = evidencia,
        };
}
