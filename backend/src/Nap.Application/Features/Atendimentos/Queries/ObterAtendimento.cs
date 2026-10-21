using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Atendimentos.Queries;

public record ObterAtendimentoQuery(Guid Id);

public class ObterAtendimentoValidator : AbstractValidator<ObterAtendimentoQuery>
{
    public ObterAtendimentoValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ObterAtendimentoHandler(IValidator<ObterAtendimentoQuery> validador, IAtendimentoQueries consultas,
    IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task<AtendimentoDetalheDto> Handle(ObterAtendimentoQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        var detalhe = await consultas.ObterAsync(query.Id, ct) ?? throw new NotFoundException("Atendimento");
        auditoria.Registrar("atendimento.consulta", "Atendimento", detalhe.Id);
        await uow.SalvarAsync(ct);
        return detalhe;
    }
}
