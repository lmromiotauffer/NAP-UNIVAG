using Nap.Application.Features.Producao;
using Nap.Domain.Producao;

namespace Nap.Application.Tests.Fakes;

public sealed class FechamentoRepositoryEmMemoria : IFechamentoRepository
{
    public List<FechamentoSemanal> Fechamentos { get; } = [];

    public Task<FechamentoSemanal?> ObterAsync(Guid profissionalId, DateOnly semana, CancellationToken ct) =>
        Task.FromResult(Fechamentos.FirstOrDefault(f => f.ProfissionalId == profissionalId && f.Semana == semana));

    public void Adicionar(FechamentoSemanal f) => Fechamentos.Add(f);

    public List<(Guid ProfissionalId, bool Exclusivo)> Bloqueios { get; } = [];

    /// <summary>Log compartilhado com o fake de agenda (ver <see cref="AgendaRepositoryEmMemoria.Travas"/>).</summary>
    public List<string>? Travas { get; init; }

    public Task BloquearAsync(Guid profissionalId, bool exclusivo, CancellationToken ct)
    {
        Bloqueios.Add((profissionalId, exclusivo));
        Travas?.Add($"fechamento:prof:{profissionalId:N}");
        return Task.CompletedTask;
    }
}
