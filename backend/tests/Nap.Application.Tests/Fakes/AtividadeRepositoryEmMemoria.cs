using Nap.Application.Comum;
using Nap.Application.Features.Atividades;
using Nap.Domain.Atividades;

namespace Nap.Application.Tests.Fakes;

public sealed class AtividadeRepositoryEmMemoria : IAtividadeRepository
{
    public List<AtividadeCurso> Atividades { get; } = [];

    public Task<AtividadeCurso?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Atividades.FirstOrDefault(a => a.Id == id));

    public Task<bool> ExisteParaCompromissoAsync(Guid compromissoId, CancellationToken ct) =>
        Task.FromResult(Atividades.Any(a => a.CompromissoId == compromissoId));

    public void Adicionar(AtividadeCurso a) => Atividades.Add(a);
}

public sealed class AtividadeQueriesFalso : IAtividadeQueries
{
    public List<AtividadeDetalheDto> Detalhes { get; } = [];
    public FiltroAtividades? FiltroRecebido { get; private set; }

    public Task<PagedResult<AtividadeDto>> ListarAsync(FiltroAtividades f, CancellationToken ct)
    {
        FiltroRecebido = f;
        return Task.FromResult(new PagedResult<AtividadeDto>([], f.Pagina, f.Tamanho, 0));
    }

    public Task<AtividadeDetalheDto?> ObterAsync(Guid id, Guid? cursoRecorte, CancellationToken ct) =>
        Task.FromResult(Detalhes.FirstOrDefault(d => d.Id == id && (cursoRecorte is null || d.CursoId == cursoRecorte)));
}
