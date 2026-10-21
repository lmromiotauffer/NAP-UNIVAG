using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Peis.Commands;

public record ArquivarPeiCommand(Guid VersaoId, string Motivo, string Versao);

public class ArquivarPeiValidator : AbstractValidator<ArquivarPeiCommand>
{
    public ArquivarPeiValidator()
    {
        RuleFor(x => x.VersaoId).NotEmpty();
        RuleFor(x => x.Motivo).Must(t => t?.Trim().Length is >= 3 and <= 500)
            .WithMessage("O motivo deve ter de 3 a 500 caracteres.");
    }
}

public sealed class ArquivarPeiHandler(IValidator<ArquivarPeiCommand> validador, IPeiRepository peis, IUsuarioAtual usuario,
    IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(ArquivarPeiCommand comando, CancellationToken ct = default)
    {
        AutorizacaoPei.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);
        var (_, versao) = await AutorizacaoPei.ObterVersaoAsync(peis, usuario, comando.VersaoId, ct);
        if (!AutorizacaoPei.PodeRevisarEPublicar(usuario)) throw new ForbiddenException();
        uow.ExigirVersao(versao, comando.Versao);

        versao.Arquivar(comando.Motivo, relogio.Agora);
        auditoria.Registrar("pei.arquivar", "VersaoPei", versao.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
