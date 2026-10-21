using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Agenda.Queries;

public record ListarEscalasQuery(Guid? ProfissionalId = null);

public sealed class ListarEscalasHandler(IEscalaRepository escalas, IUsuarioAtual usuario)
{
    public Task<IReadOnlyList<EscalaProfissionalDto>> Handle(ListarEscalasQuery query, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        return escalas.ListarAsync(query.ProfissionalId, ct);
    }
}
