using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Peis.Queries;

public record ObterVersaoPeiQuery(Guid VersaoId);

public class ObterVersaoPeiValidator : AbstractValidator<ObterVersaoPeiQuery>
{
    public ObterVersaoPeiValidator() => RuleFor(x => x.VersaoId).NotEmpty();
}

public sealed class ObterVersaoPeiHandler(IValidator<ObterVersaoPeiQuery> validador, IPeiQueries consultas,
    IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow, IRelogio relogio)
{
    public async Task<VersaoPeiDetalheDto> Handle(ObterVersaoPeiQuery query, CancellationToken ct = default)
    {
        var escopo = EscopoPeis.Para(usuario);
        await validador.ValidarAsync(query, ct);
        var detalhe = await consultas.ObterVersaoAsync(query.VersaoId, escopo, usuario, ct)
            ?? throw new NotFoundException("Versão do PEI");
        auditoria.Registrar("pei.consulta", "VersaoPei", detalhe.Id);
        await uow.SalvarAsync(ct);
        return detalhe with { ForaDaVigencia = VigenciaPei.ForaDaVigencia(detalhe.Estado, detalhe.Conteudo.VigenciaFim, relogio.Hoje) };
    }
}
