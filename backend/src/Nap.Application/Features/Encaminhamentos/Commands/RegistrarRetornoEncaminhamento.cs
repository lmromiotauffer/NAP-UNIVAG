using FluentValidation;
using Nap.Application.Comum;
using Nap.Domain.Encaminhamentos;

namespace Nap.Application.Features.Encaminhamentos.Commands;

public record RegistrarRetornoEncaminhamentoCommand(Guid Id, AcaoRetorno Acao, DateOnly? Data, string? Texto, string Versao);

public class RegistrarRetornoEncaminhamentoValidator : AbstractValidator<RegistrarRetornoEncaminhamentoCommand>
{
    public RegistrarRetornoEncaminhamentoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Acao).IsInEnum()
            .NotEqual(AcaoRetorno.Cancelamento).WithMessage("Para cancelar, use a operação de cancelamento.");
    }
}

public sealed class RegistrarRetornoEncaminhamentoHandler(IValidator<RegistrarRetornoEncaminhamentoCommand> validador,
    IEncaminhamentoRepository encaminhamentos, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria,
    IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(RegistrarRetornoEncaminhamentoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);

        var e = await encaminhamentos.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Encaminhamento");
        if (!AutorizacaoEncaminhamento.PodeVer(usuario, e)) throw new NotFoundException("Encaminhamento");
        if (!AutorizacaoEncaminhamento.PodeRegistrarRetorno(usuario, e)) throw new ForbiddenException();
        uow.ExigirVersao(e, comando.Versao);

        e.RegistrarRetorno(comando.Acao, comando.Data, comando.Texto, usuario.Id, relogio.Hoje, relogio.Agora);
        auditoria.Registrar("encaminhamento.retorno", "Encaminhamento", e.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
