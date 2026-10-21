using Nap.Application.Features.Agenda;
using Nap.Domain.Agenda;

namespace Nap.Application.Tests.Fakes;

public sealed class FeriadoRepositoryEmMemoria : IFeriadoRepository
{
    public List<Feriado> Feriados { get; } = [];
    /// <summary>Devolvidos para qualquer data.</summary>
    public List<ConflitoFeriadoDto> Conflitos { get; } = [];

    public Task<Feriado?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Feriados.FirstOrDefault(f => f.Id == id));

    public Task<Feriado?> ObterAtivoAsync(DateOnly data, CancellationToken ct) =>
        Task.FromResult(Feriados.FirstOrDefault(f => f.Data == data && f.Ativo));

    public Task<IReadOnlyList<Feriado>> ListarAtivosAsync(DateOnly de, DateOnly ate, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Feriado>>(Feriados.Where(f => f.Ativo && f.Data >= de && f.Data <= ate).OrderBy(f => f.Data).ToList());

    public Task<IReadOnlyList<ConflitoFeriadoDto>> ListarConflitosAsync(DateOnly data, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ConflitoFeriadoDto>>(Conflitos.ToList());

    public void Adicionar(Feriado f) => Feriados.Add(f);
}
