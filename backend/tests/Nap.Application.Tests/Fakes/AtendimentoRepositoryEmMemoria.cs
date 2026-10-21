using Nap.Application.Comum;
using Nap.Application.Features.Atendimentos;
using Nap.Domain.Atendimentos;

namespace Nap.Application.Tests.Fakes;

public sealed class AtendimentoRepositoryEmMemoria : IAtendimentoRepository
{
    public List<Atendimento> Atendimentos { get; } = [];

    public Task<Atendimento?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Atendimentos.FirstOrDefault(a => a.Id == id));

    public Task<bool> ExisteParaCompromissoAsync(Guid compromissoId, CancellationToken ct) =>
        Task.FromResult(Atendimentos.Any(a => a.CompromissoId == compromissoId));

    public void Adicionar(Atendimento a) => Atendimentos.Add(a);
}

public sealed class AtendimentoQueriesFalso : IAtendimentoQueries
{
    public AtendimentoDetalheDto? Detalhe { get; set; }

    public Task<PagedResult<AtendimentoListaDto>> ListarAsync(FiltroAtendimentos f, CancellationToken ct) =>
        Task.FromResult(new PagedResult<AtendimentoListaDto>([], f.Pagina, f.Tamanho, 0));

    public Task<AtendimentoDetalheDto?> ObterAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Detalhe is not null && Detalhe.Id == id ? Detalhe : null);
}
