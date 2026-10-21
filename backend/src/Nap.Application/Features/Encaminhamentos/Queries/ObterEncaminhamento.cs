using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Encaminhamentos.Queries;

public record ObterEncaminhamentoQuery(Guid Id);

public class ObterEncaminhamentoValidator : AbstractValidator<ObterEncaminhamentoQuery>
{
    public ObterEncaminhamentoValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ObterEncaminhamentoHandler(IValidator<ObterEncaminhamentoQuery> validador, IEncaminhamentoQueries consultas,
    IUsuarioAtual usuario, IRelogio relogio, IConfiguracaoAlertas alertas, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task<EncaminhamentoDetalheDto> Handle(ObterEncaminhamentoQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        var escopo = EscopoEncaminhamentos.Para(usuario);
        var detalhe = await consultas.ObterAsync(query.Id, escopo, usuario, relogio.Hoje, (await alertas.ObterDtoAsync(ct)).DiasSemAcolhimento, ct)
            ?? throw new NotFoundException("Encaminhamento");
        auditoria.Registrar("encaminhamento.consulta", "Encaminhamento", detalhe.Id);
        await uow.SalvarAsync(ct);
        return detalhe;
    }
}
