using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Encaminhamentos.Commands;

public record CancelarEncaminhamentoCommand(Guid Id, string Justificativa, string Versao);

public class CancelarEncaminhamentoValidator : AbstractValidator<CancelarEncaminhamentoCommand>
{
    public CancelarEncaminhamentoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Justificativa).NotEmpty().WithMessage("Informe a justificativa.").Length(3, 1000);
    }
}

public sealed class CancelarEncaminhamentoHandler(IValidator<CancelarEncaminhamentoCommand> validador,
    IEncaminhamentoRepository encaminhamentos, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria,
    IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(CancelarEncaminhamentoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);

        var e = await encaminhamentos.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Encaminhamento");
        if (!AutorizacaoEncaminhamento.PodeVer(usuario, e)) throw new NotFoundException("Encaminhamento");
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();
        uow.ExigirVersao(e, comando.Versao);

        e.Cancelar(comando.Justificativa, usuario.Id, relogio.Hoje, relogio.Agora);
        auditoria.Registrar("encaminhamento.cancelamento", "Encaminhamento", e.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
