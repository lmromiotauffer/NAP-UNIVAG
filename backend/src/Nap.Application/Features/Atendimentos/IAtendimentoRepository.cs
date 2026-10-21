using Nap.Domain.Atendimentos;

namespace Nap.Application.Features.Atendimentos;

public interface IAtendimentoRepository
{
    Task<Atendimento?> ObterAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteParaCompromissoAsync(Guid compromissoId, CancellationToken ct);
    void Adicionar(Atendimento a);
}
