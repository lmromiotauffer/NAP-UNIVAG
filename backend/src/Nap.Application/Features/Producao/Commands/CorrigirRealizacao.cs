using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Features.Producao.Commands;

public record CorrigirRealizacaoCommand(Guid CompromissoId, string? Versao, DateTimeOffset Inicio, DateTimeOffset Fim, string Justificativa);

public class CorrigirRealizacaoValidator : AbstractValidator<CorrigirRealizacaoCommand>
{
    public CorrigirRealizacaoValidator()
    {
        RuleFor(x => x.CompromissoId).NotEmpty();
        RuleFor(x => x.Fim).GreaterThan(x => x.Inicio).WithMessage("O horário de término deve ser posterior ao de início.")
            .Must((x, fim) => fim - x.Inicio <= TimeSpan.FromHours(12)).WithMessage("A duração não pode ultrapassar 12 horas.");
        RuleFor(x => x.Justificativa).NotEmpty().WithMessage("Informe a justificativa da correção.")
            .Must(j => j is null || j.Trim().Length is >= 5 and <= 500).WithMessage("A justificativa deve ter de 5 a 500 caracteres.");
    }
}

public sealed class CorrigirRealizacaoHandler(IValidator<CorrigirRealizacaoCommand> validador, IAgendaRepository agenda,
    IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache,
    ServicoFechamento fechamento)
{
    public async Task Handle(CorrigirRealizacaoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        var compromisso = await agenda.ObterAsync(comando.CompromissoId, ct) ?? throw new NotFoundException("Compromisso");
        if (!usuario.EhCoordenacao() && compromisso.ProfissionalId != usuario.Id) throw new ForbiddenException();
        uow.ExigirVersao(compromisso, comando.Versao ?? string.Empty);

        var novo = IntervaloHorario.Criar(comando.Inicio, comando.Fim);
        var anterior = compromisso.IntervaloEfetivo;
        await uow.EmTransacaoAsync(async tct =>
        {
            // O efetivo atual e o novo podem cair em semanas diferentes; as duas precisam estar abertas.
            await fechamento.ExigirSemanaAbertaAsync(compromisso.ProfissionalId, anterior.Inicio, tct);
            await fechamento.ExigirSemanaAbertaAsync(compromisso.ProfissionalId, novo.Inicio, tct);
            await agenda.GarantirSemConflitoAsync(compromisso.ProfissionalId, compromisso.AlunoId, novo, compromisso.Id, true, tct);
            compromisso.CorrigirRealizacao(novo, comando.Justificativa, usuario.Id, relogio.Agora);
            auditoria.Registrar("compromisso.realizacao.correcao", "Compromisso", compromisso.Id);
            await uow.SalvarAsync(tct);
            return compromisso.Id;
        }, ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
