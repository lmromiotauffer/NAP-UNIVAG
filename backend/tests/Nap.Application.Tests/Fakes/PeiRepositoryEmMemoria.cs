using Nap.Application.Features.Peis;
using Nap.Domain.Peis;

namespace Nap.Application.Tests.Fakes;

public sealed class PeiRepositoryEmMemoria : IPeiRepository
{
    public List<Pei> Peis { get; } = [];
    public List<VersaoPei> VersoesRemovidas { get; } = [];

    public Task<Pei?> ObterAsync(Guid peiId, CancellationToken ct) => Task.FromResult(Peis.FirstOrDefault(p => p.Id == peiId));

    public Task<Pei?> ObterPorVersaoAsync(Guid versaoId, CancellationToken ct) =>
        Task.FromResult(Peis.FirstOrDefault(p => p.Versoes.Any(v => v.Id == versaoId)));

    public void Adicionar(Pei p) => Peis.Add(p);

    public void RemoverVersao(VersaoPei v) => VersoesRemovidas.Add(v);
}

public sealed class AcessoAssinaturaRepositoryEmMemoria : IAcessoAssinaturaRepository
{
    public List<AcessoAssinaturaPei> Acessos { get; } = [];

    public Task<AcessoAssinaturaPei?> ObterPorTokenHashAsync(string tokenHash, CancellationToken ct) =>
        Task.FromResult(Acessos.FirstOrDefault(a => a.TokenHash == tokenHash));

    public Task InvalidarAbertosDaVersaoAsync(Guid versaoId, DateTimeOffset agora, CancellationToken ct)
    {
        foreach (var a in Acessos.Where(a => a.VersaoId == versaoId && a.UsadoEm is null && a.InvalidadoEm is null))
            a.Invalidar(agora);
        return Task.CompletedTask;
    }

    public void Adicionar(AcessoAssinaturaPei a) => Acessos.Add(a);

    /// <summary>Simula a releitura do banco: o uso não gravado (outra requisição ganhou por motivo alheio ao link) some.</summary>
    public bool RecarregarDesfazUso { get; init; }

    public Task<bool> RecarregarAsync(AcessoAssinaturaPei a, CancellationToken ct)
    {
        if (RecarregarDesfazUso) typeof(AcessoAssinaturaPei).GetProperty(nameof(AcessoAssinaturaPei.UsadoEm))!.SetValue(a, null);
        return Task.FromResult(Acessos.Contains(a));
    }
}
