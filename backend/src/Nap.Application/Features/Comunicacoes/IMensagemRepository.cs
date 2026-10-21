using Nap.Domain.Comunicacoes;

namespace Nap.Application.Features.Comunicacoes;

public interface IMensagemRepository
{
    Task<Mensagem?> ObterAsync(Guid id, CancellationToken ct);
    void Adicionar(Mensagem m);
    void Remover(Mensagem m);
}
