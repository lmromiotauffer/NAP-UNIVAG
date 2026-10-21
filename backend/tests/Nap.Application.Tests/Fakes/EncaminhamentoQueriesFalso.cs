using Nap.Application.Comum;
using Nap.Application.Features.Encaminhamentos;

namespace Nap.Application.Tests.Fakes;

public sealed class EncaminhamentoQueriesFalso : IEncaminhamentoQueries
{
    public EncaminhamentoDetalheDto? Detalhe { get; set; }

    public Task<PagedResult<EncaminhamentoListaDto>> ListarAsync(FiltroEncaminhamentos f, EscopoEncaminhamentos e, DateOnly hoje,
        int diasSemAcolhimento, CancellationToken ct) =>
        Task.FromResult(new PagedResult<EncaminhamentoListaDto>([], f.Pagina, f.Tamanho, 0));

    public Task<EncaminhamentoDetalheDto?> ObterAsync(Guid id, EscopoEncaminhamentos e, IUsuarioAtual u, DateOnly hoje,
        int diasSemAcolhimento, CancellationToken ct) =>
        Task.FromResult(Detalhe is not null && Detalhe.Id == id ? Detalhe : null);
}
