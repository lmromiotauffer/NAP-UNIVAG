using Nap.Application.Comum;
using Nap.Domain.Encaminhamentos;

namespace Nap.Application.Features.Encaminhamentos;

/// <summary><c>De</c>/<c>Ate</c> filtram <c>DataEncaminhamento</c>.</summary>
public record FiltroEncaminhamentos(Guid? AlunoId, Guid? CursoId, Especialidade? Especialidade, DestinoEncaminhamento? Destino,
    EstadoEncaminhamento? Estado, DateOnly? De, DateOnly? Ate, bool? SemAcolhimentoNoPrazo, int Pagina, int Tamanho);

public interface IEncaminhamentoQueries
{
    Task<PagedResult<EncaminhamentoListaDto>> ListarAsync(FiltroEncaminhamentos f, EscopoEncaminhamentos e, DateOnly hoje,
        int diasSemAcolhimento, CancellationToken ct);

    /// <summary><c>null</c> quando não existe ou está fora do escopo.</summary>
    Task<EncaminhamentoDetalheDto?> ObterAsync(Guid id, EscopoEncaminhamentos e, IUsuarioAtual u, DateOnly hoje,
        int diasSemAcolhimento, CancellationToken ct);
}
