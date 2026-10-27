using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Comunicacoes.Queries;

/// <summary>Não marca a mensagem como lida: isso é uma ação explícita do destinatário.</summary>
public record ObterMensagemQuery(Guid Id);

public class ObterMensagemValidator : AbstractValidator<ObterMensagemQuery>
{
    public ObterMensagemValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ObterMensagemHandler(IValidator<ObterMensagemQuery> validador, IComunicacaoQueries consultas, IUsuarioAtual usuario)
{
    public async Task<MensagemDetalheDto> Handle(ObterMensagemQuery query, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);
        await validador.ValidarAsync(query, ct);
        return await consultas.ObterAsync(query.Id, usuario, ct) ?? throw new NotFoundException("Mensagem");
    }
}
