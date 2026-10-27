using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Features.Producao.Commands;

public record RegistrarRealizacaoCommand(Guid CompromissoId, string Versao, DateTimeOffset Inicio, DateTimeOffset Fim);

public class RegistrarRealizacaoValidator : AbstractValidator<RegistrarRealizacaoCommand>
{
    public RegistrarRealizacaoValidator()
    {
        RuleFor(x => x.CompromissoId).NotEmpty();
        RuleFor(x => x.Fim).IntervaloValido(x => x.Inicio);
    }
}

public sealed class RegistrarRealizacaoHandler(IValidator<RegistrarRealizacaoCommand> validador, IAgendaRepository agenda,
    IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache,
    ServicoFechamento fechamento, ServicoEscala escala)
{
    public async Task Handle(RegistrarRealizacaoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        var compromisso = await agenda.ObterAsync(comando.CompromissoId, ct) ?? throw new NotFoundException("Compromisso");
        if (!usuario.EhCoordenacao() && compromisso.ProfissionalId != usuario.Id) throw new ForbiddenException();
        uow.ExigirVersao(compromisso, comando.Versao);

        var efetivo = IntervaloHorario.Criar(comando.Inicio, comando.Fim);
        await uow.EmTransacaoAsync(async tct =>
        {
            // O horário agendado e o efetivo podem cair em semanas diferentes.
            await fechamento.ExigirSemanaAbertaAsync(compromisso.ProfissionalId, compromisso.Intervalo, tct);
            await fechamento.ExigirSemanaAbertaAsync(compromisso.ProfissionalId, efetivo, tct);
            await agenda.GarantirSemConflitoAsync(compromisso.ProfissionalId, compromisso.AlunoId, efetivo, compromisso.Id, true, tct);
            // Mesma regra do agendamento: um início novo passa pela escala (fora dela, só com autorização).
            if (efetivo.Inicio != compromisso.Intervalo.Inicio)
                await escala.ExigirDentroDaEscalaAsync(compromisso.ProfissionalId, efetivo.Inicio, tct);
            compromisso.RegistrarRealizacao(efetivo, usuario.Id, relogio.Agora);
            auditoria.Registrar("compromisso.realizacao", "Compromisso", compromisso.Id);
            await uow.SalvarAsync(tct);
            return compromisso.Id;
        }, ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
