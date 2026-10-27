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
    /// <summary>Devolução da versão: a assinatura deixa de valer, mas fica como registro do que foi assinado.</summary>
    public DateTimeOffset? InvalidadaEm { get; private set; }

    public bool Valida => InvalidadaEm is null;

    private AssinaturaPei() { } // ORM

    internal static AssinaturaPei Criar(PapelAssinatura papel, Guid? signatarioId, string nome, string hashConteudo,
        string metodo, string evidencia, DateTimeOffset em) =>
        new()
        {
            Papel = papel, SignatarioId = signatarioId, NomeSignatario = nome, Em = em, Metodo = metodo,
            HashConteudo = hashConteudo, Evidencia = evidencia,
        };

    internal void Invalidar(DateTimeOffset em) => InvalidadaEm ??= em;
}
