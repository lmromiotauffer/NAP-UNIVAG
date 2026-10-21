using Nap.Domain.Peis;

namespace Nap.Application.Comum;

public record EvidenciaAssinatura(string Metodo, string Evidencia);

/// <summary>Porta de assinatura do PEI; um provedor certificado entra atrás desta interface.</summary>
public interface IServicoAssinatura
{
    Task<EvidenciaAssinatura> AssinarAsync(Guid versaoId, PapelAssinatura papel, string nomeSignatario,
        string hashConteudo, DateTimeOffset instante, CancellationToken ct);
}
