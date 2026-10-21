using Nap.Application.Features.Encaminhamentos;
using Nap.Domain.Encaminhamentos;

namespace Nap.Application.Tests.Fakes;

public sealed class EncaminhamentoRepositoryEmMemoria : IEncaminhamentoRepository
{
    public List<Encaminhamento> Encaminhamentos { get; } = [];

    public Task<Encaminhamento?> ObterAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Encaminhamentos.FirstOrDefault(e => e.Id == id));

    public void Adicionar(Encaminhamento e) => Encaminhamentos.Add(e);
}
