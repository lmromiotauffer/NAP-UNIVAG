using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Comunicacoes.Queries;

/// <summary><c>Busca</c> procura em assunto e corpo, sempre dentro da caixa do usuário.</summary>
public record ListarCaixaQuery(CaixaMensagens Caixa = CaixaMensagens.Entrada, string? Busca = null, Guid? AlunoId = null,
    int Pagina = 1, int Tamanho = 20, Guid? EncaminhamentoId = null);

public class ListarCaixaValidator : AbstractValidator<ListarCaixaQuery>
{
    public ListarCaixaValidator()
    {
        RuleFor(x => x.Caixa).IsInEnum();
        RuleFor(x => x.Busca).MaximumLength(100);
        RuleFor(x => x.Pagina).InclusiveBetween(1, 100_000);
        RuleFor(x => x.Tamanho).InclusiveBetween(1, 100);
    }
}

public sealed class ListarCaixaHandler(IValidator<ListarCaixaQuery> validador, IComunicacaoQueries consultas, IUsuarioAtual usuario)
{
    public async Task<PagedResult<MensagemListaDto>> Handle(ListarCaixaQuery query, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);
        await validador.ValidarAsync(query, ct);
        return await consultas.ListarAsync(usuario, query.Caixa, query.Busca, query.AlunoId, query.EncaminhamentoId,
            query.Pagina, query.Tamanho, ct);
    }
}
