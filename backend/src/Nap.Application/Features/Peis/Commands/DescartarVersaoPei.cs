using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Peis.Commands;

/// <summary>Exclusão física de um rascunho esquecido (a partir da v2), para não bloquear novas versões. Exige a versão
/// vista: a exclusão é definitiva e não pode apagar texto gravado depois por outra pessoa.</summary>
public record DescartarVersaoPeiCommand(Guid VersaoId, string Versao);

public class DescartarVersaoPeiValidator : AbstractValidator<DescartarVersaoPeiCommand>
{
    public DescartarVersaoPeiValidator() => RuleFor(x => x.VersaoId).NotEmpty();
}

public sealed class DescartarVersaoPeiHandler(IValidator<DescartarVersaoPeiCommand> validador, IPeiRepository peis,
    IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(DescartarVersaoPeiCommand comando, CancellationToken ct = default)
    {
        AutorizacaoPei.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);
        var (pei, versao) = await AutorizacaoPei.ObterVersaoAsync(peis, usuario, comando.VersaoId, ct);
        if (!AutorizacaoPei.PodeElaborar(usuario)) throw new ForbiddenException();
        uow.ExigirVersao(versao, comando.Versao);

        pei.DescartarVersao(versao);
        peis.RemoverVersao(versao);
        auditoria.Registrar("pei.versao.descartar", "VersaoPei", versao.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
