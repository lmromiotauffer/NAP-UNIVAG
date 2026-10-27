using Nap.Domain.Comunicacoes;

namespace Nap.Application.Features.Comunicacoes;

public interface IMensagemRepository
{
    Task<Mensagem?> ObterAsync(Guid id, CancellationToken ct);
    void Adicionar(Mensagem m);
    void Remover(Mensagem m);

    /// <summary>Relê do banco os valores atuais; falso se a mensagem não existe mais.</summary>
    Task<bool> RecarregarAsync(Mensagem m, CancellationToken ct);
}
