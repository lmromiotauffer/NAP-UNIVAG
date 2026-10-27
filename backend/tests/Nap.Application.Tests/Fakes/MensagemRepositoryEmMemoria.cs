using Nap.Application.Features.Comunicacoes;
using Nap.Domain.Comunicacoes;

namespace Nap.Application.Tests.Fakes;

public sealed class MensagemRepositoryEmMemoria : IMensagemRepository
{
    public List<Mensagem> Mensagens { get; } = [];

    public Task<Mensagem?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Mensagens.FirstOrDefault(m => m.Id == id));

    public void Adicionar(Mensagem m) => Mensagens.Add(m);

    public void Remover(Mensagem m) => Mensagens.Remove(m);

    public Task<bool> RecarregarAsync(Mensagem m, CancellationToken ct) => Task.FromResult(Mensagens.Contains(m));
}
