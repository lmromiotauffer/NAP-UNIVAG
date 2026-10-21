using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Agenda.Commands;

/// <summary>Encerrar (ou reabrir, com <c>VigenciaFim</c> nulo) a vigência da escala.</summary>
public record AtualizarVigenciaEscalaCommand(Guid Id, DateOnly? VigenciaFim, string Versao);

public class AtualizarVigenciaEscalaValidator : AbstractValidator<AtualizarVigenciaEscalaCommand>
{
    public AtualizarVigenciaEscalaValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class AtualizarVigenciaEscalaHandler(IValidator<AtualizarVigenciaEscalaCommand> validador, IEscalaRepository escalas,
    IAgendaRepository agenda, IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task Handle(AtualizarVigenciaEscalaCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(comando, ct);
        var escala = await escalas.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Escala");
        uow.ExigirVersao(escala, comando.Versao);

        escala.AtualizarVigenciaFim(comando.VigenciaFim);
        await uow.EmTransacaoAsync(async tct =>
        {
            // Prolongar o fim pode encostar na escala seguinte.
            await CriarEscalaHandler.ExigirSemSobreposicaoAsync(escalas, agenda, escala, tct);
            auditoria.Registrar("escala.atualizar", "EscalaProfissional", escala.Id);
            await uow.SalvarAsync(tct);
            return escala.Id;
        }, ct);
    }
}
