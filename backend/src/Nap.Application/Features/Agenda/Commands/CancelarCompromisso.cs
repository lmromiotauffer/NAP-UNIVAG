using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Producao;
using Nap.Domain.Agenda;

namespace Nap.Application.Features.Agenda.Commands;

public record CancelarCompromissoCommand(Guid Id, string Motivo, string Versao, EscopoGrupo Escopo = EscopoGrupo.Minha);

public class CancelarCompromissoValidator : AbstractValidator<CancelarCompromissoCommand>
{
    public CancelarCompromissoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Escopo).IsInEnum();
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("Informe o motivo.")
            .Must(m => m is null || m.Trim().Length is >= 3 and <= 500).WithMessage("O motivo deve ter de 3 a 500 caracteres.");
    }
}

public sealed class CancelarCompromissoHandler(IValidator<CancelarCompromissoCommand> validador, IAgendaRepository agenda,
    IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache,
    ServicoFechamento fechamento)
{
    public async Task Handle(CancelarCompromissoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        var compromisso = await agenda.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Compromisso");
        var instancias = await agenda.InstanciasDoEscopoAsync(compromisso, comando.Escopo, usuario, ct, minhaEmGrupo: true);
        uow.ExigirVersao(compromisso, comando.Versao);

        await uow.EmTransacaoAsync(async tct =>
        {
            // Cancelar compromisso que já terminou altera a produção da semana (ex.: "Não realizado"); bloqueio, a qualquer
            // momento, porque a capacidade desconta o bloqueio inteiro, inclusive o trecho ainda por vir.
            foreach (var c in instancias.OrderBy(c => c.ProfissionalId.ToString("N"), StringComparer.Ordinal))
                if (c.Tipo == TipoCompromisso.Bloqueio || c.Intervalo.Fim <= relogio.Agora)
                    await fechamento.ExigirSemanaAbertaAsync(c.ProfissionalId, c.Intervalo, tct);
            // Trava a agenda de cada participante: serializa com a edição do grupo (as irmãs não passam pelo If-Match).
            await agenda.BloquearAgendasAsync([.. instancias.Select(c => c.ProfissionalId)], tct);
            foreach (var c in instancias)
            {
                c.Cancelar(comando.Motivo);
                auditoria.Registrar("agenda.cancelamento", "Compromisso", c.Id);
            }
            await uow.SalvarAsync(tct);
            return compromisso.Id;
        }, ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
