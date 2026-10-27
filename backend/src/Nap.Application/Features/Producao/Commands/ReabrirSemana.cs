using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Producao.Commands;

public record ReabrirSemanaCommand(Guid ProfissionalId, DateOnly Semana, string Justificativa, string Versao);

public class ReabrirSemanaValidator : AbstractValidator<ReabrirSemanaCommand>
{
    public ReabrirSemanaValidator()
    {
        RuleFor(x => x.ProfissionalId).NotEmpty();
        RuleFor(x => x.Semana).Must(s => s.DayOfWeek == DayOfWeek.Monday).WithMessage("A semana deve ser identificada pela segunda-feira.");
        RuleFor(x => x.Justificativa).NotEmpty().WithMessage("Informe a justificativa da reabertura.")
            .Must(j => j is null || j.Trim().Length is >= 5 and <= 500).WithMessage("A justificativa deve ter de 5 a 500 caracteres.");
    }
}

public sealed class ReabrirSemanaHandler(IValidator<ReabrirSemanaCommand> validador, IFechamentoRepository fechamentos,
    IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(ReabrirSemanaCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhCoordenacao()) throw new ForbiddenException();

        await uow.EmTransacaoAsync(async tct =>
        {
            await fechamentos.BloquearAsync(comando.ProfissionalId, true, tct);
            var fechamento = await fechamentos.ObterAsync(comando.ProfissionalId, comando.Semana, tct)
                ?? throw new NotFoundException("Fechamento da semana");
            uow.ExigirVersao(fechamento, comando.Versao);

            fechamento.Reabrir(comando.Justificativa, usuario.Id, relogio.Agora);
            auditoria.Registrar("producao.semana.reabrir", "FechamentoSemanal", fechamento.Id);
            await uow.SalvarAsync(tct);
            return fechamento.Id;
        }, ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
