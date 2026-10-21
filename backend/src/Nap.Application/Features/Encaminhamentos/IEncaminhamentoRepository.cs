using Nap.Domain.Encaminhamentos;

namespace Nap.Application.Features.Encaminhamentos;

public interface IEncaminhamentoRepository
{
    /// <summary>Carrega o encaminhamento com suas evoluções.</summary>
    Task<Encaminhamento?> ObterAsync(Guid id, CancellationToken ct);
    void Adicionar(Encaminhamento e);
}
