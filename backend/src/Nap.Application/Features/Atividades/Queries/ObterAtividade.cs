using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Atividades.Queries;

public record ObterAtividadeQuery(Guid Id);

public class ObterAtividadeValidator : AbstractValidator<ObterAtividadeQuery>
{
    public ObterAtividadeValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ObterAtividadeHandler(IValidator<ObterAtividadeQuery> validador, IAtividadeQueries consultas, IUsuarioAtual usuario)
{
    public async Task<AtividadeDetalheDto> Handle(ObterAtividadeQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        var recorte = RecorteDeCurso.Para(usuario);
        var detalhe = await consultas.ObterAsync(query.Id, recorte, ct) ?? throw new NotFoundException("Atividade");
        var podeCorrigir = usuario.EhEquipeNap() && (usuario.EhCoordenacao() || detalhe.ProfissionalId == usuario.Id);
        return detalhe with { PodeCorrigir = podeCorrigir };
    }
}
