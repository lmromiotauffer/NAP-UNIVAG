using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Notificacoes.Queries;

public record ListarNotificacoesQuery(bool NaoLidas = false, int Pagina = 1, int Tamanho = 20);

public class ListarNotificacoesValidator : AbstractValidator<ListarNotificacoesQuery>
{
    public ListarNotificacoesValidator()
    {
        RuleFor(x => x.Pagina).InclusiveBetween(1, 100_000);
        RuleFor(x => x.Tamanho).InclusiveBetween(1, 100);
    }
}

public sealed class ListarNotificacoesHandler(IValidator<ListarNotificacoesQuery> validador, INotificacaoQueries consultas,
    IUsuarioAtual usuario)
{
    public async Task<PagedResult<NotificacaoDto>> Handle(ListarNotificacoesQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        return await consultas.ListarAsync(usuario.Id, query.NaoLidas, query.Pagina, query.Tamanho, ct);
    }
}
