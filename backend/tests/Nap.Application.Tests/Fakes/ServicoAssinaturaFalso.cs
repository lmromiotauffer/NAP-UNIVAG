using Nap.Application.Comum;
using Nap.Domain.Peis;

namespace Nap.Application.Tests.Fakes;

public sealed class ServicoAssinaturaFalso : IServicoAssinatura
{
    public Task<EvidenciaAssinatura> AssinarAsync(Guid versaoId, PapelAssinatura papel, string nomeSignatario,
        string hashConteudo, DateTimeOffset instante, CancellationToken ct) =>
        Task.FromResult(new EvidenciaAssinatura("Demonstrativa", $"evidencia-{papel}-{hashConteudo}"));
}
