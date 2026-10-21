using Nap.Application.Comum;
using Nap.Domain.Atendimentos;

namespace Nap.Application.Features.Atendimentos;

public record FiltroAtendimentos(Guid? AlunoId, Guid? ProfissionalId, EstadoAtendimento? Estado, TipoAtendimento? Tipo,
    DateOnly? De, DateOnly? Ate, int Pagina, int Tamanho);

public interface IAtendimentoQueries
{
    Task<PagedResult<AtendimentoListaDto>> ListarAsync(FiltroAtendimentos f, CancellationToken ct);
    Task<AtendimentoDetalheDto?> ObterAsync(Guid id, CancellationToken ct);
}
