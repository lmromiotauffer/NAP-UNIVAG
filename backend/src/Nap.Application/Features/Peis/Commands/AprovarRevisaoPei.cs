using FluentValidation;
using Nap.Application.Comum;
using Nap.Domain.Peis;

namespace Nap.Application.Features.Peis.Commands;

public record AprovarRevisaoPeiCommand(Guid VersaoId, string Versao);

public class AprovarRevisaoPeiValidator : AbstractValidator<AprovarRevisaoPeiCommand>
{
    public AprovarRevisaoPeiValidator() => RuleFor(x => x.VersaoId).NotEmpty();
}

public sealed class AprovarRevisaoPeiHandler(IValidator<AprovarRevisaoPeiCommand> validador, IPeiRepository peis,
    IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(AprovarRevisaoPeiCommand comando, CancellationToken ct = default)
    {
        AutorizacaoPei.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);
        var (_, versao) = await AutorizacaoPei.ObterVersaoAsync(peis, usuario, comando.VersaoId, ct);
        if (!AutorizacaoPei.PodeRevisarEPublicar(usuario)) throw new ForbiddenException();
        uow.ExigirVersao(versao, comando.Versao);

        versao.AprovarRevisao(usuario.Id, relogio.Agora);
        auditoria.Registrar("pei.revisao.aprovar", "VersaoPei", versao.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
