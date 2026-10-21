using Nap.Application.Comum;
using Nap.Application.Features.Alunos;

namespace Nap.Application.Tests.Fakes;

public sealed class AlunoQueriesFalso : IAlunoQueries
{
    public EscopoAlunos? EscopoRecebido { get; private set; }
    public FiltroAlunos? FiltroRecebido { get; private set; }
    public AlunoFichaDto? Ficha { get; set; }
    public int TotalLinhaDoTempo { get; set; }

    public Task<PagedResult<AlunoListaDto>> ListarAsync(FiltroAlunos f, EscopoAlunos e, CancellationToken ct)
    {
        EscopoRecebido = e;
        FiltroRecebido = f;
        return Task.FromResult(new PagedResult<AlunoListaDto>([], f.Pagina, f.Tamanho, 0));
    }

    public Task<AlunoFichaDto?> ObterFichaAsync(Guid id, EscopoAlunos e, CancellationToken ct)
    {
        EscopoRecebido = e;
        return Task.FromResult(Ficha is not null && Ficha.Id == id ? Ficha : null);
    }

    // Mesmo escopo da ficha: sem ficha para o id, a linha do tempo é nula.
    public Task<PagedResult<EventoLinhaDoTempoDto>?> LinhaDoTempoAsync(Guid alunoId, EscopoAlunos e, int pagina, int tamanho, CancellationToken ct)
    {
        EscopoRecebido = e;
        return Task.FromResult(Ficha is not null && Ficha.Id == alunoId
            ? new PagedResult<EventoLinhaDoTempoDto>([], pagina, tamanho, TotalLinhaDoTempo) : null);
    }
}
