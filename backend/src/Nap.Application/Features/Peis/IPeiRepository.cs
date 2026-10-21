using Nap.Domain.Peis;

namespace Nap.Application.Features.Peis;

/// <summary>Carrega o PEI com versões, assinaturas e ciências.</summary>
public interface IPeiRepository
{
    Task<Pei?> ObterAsync(Guid peiId, CancellationToken ct);
    Task<Pei?> ObterPorVersaoAsync(Guid versaoId, CancellationToken ct);
    void Adicionar(Pei p);
    /// <summary>Exclusão física da versão já retirada do agregado por <c>Pei.DescartarVersao</c>.</summary>
    void RemoverVersao(VersaoPei v);
}
