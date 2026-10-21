using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Peis.Commands;

public record EnviarPeiParaRevisaoCommand(Guid VersaoId, string Versao);

public class EnviarPeiParaRevisaoValidator : AbstractValidator<EnviarPeiParaRevisaoCommand>
{
    public EnviarPeiParaRevisaoValidator() => RuleFor(x => x.VersaoId).NotEmpty();
}

public sealed class EnviarPeiParaRevisaoHandler(IValidator<EnviarPeiParaRevisaoCommand> validador, IPeiRepository peis,
    IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(EnviarPeiParaRevisaoCommand comando, CancellationToken ct = default)
    {
        AutorizacaoPei.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);
        var (_, versao) = await AutorizacaoPei.ObterVersaoAsync(peis, usuario, comando.VersaoId, ct);
        if (!AutorizacaoPei.PodeElaborar(usuario)) throw new ForbiddenException();
        uow.ExigirVersao(versao, comando.Versao);

        versao.EnviarParaRevisao(relogio.Agora);
        auditoria.Registrar("pei.revisao.enviar", "VersaoPei", versao.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
