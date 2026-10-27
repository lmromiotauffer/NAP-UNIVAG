using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Producao;
using Nap.Domain.Agenda;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Features.Agenda.Commands;

public record RemarcarCompromissoCommand(Guid Id, DateTimeOffset Inicio, DateTimeOffset Fim, string Versao,
    EscopoGrupo Escopo = EscopoGrupo.Minha);

public class RemarcarCompromissoValidator : AbstractValidator<RemarcarCompromissoCommand>
{
    public RemarcarCompromissoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Escopo).IsInEnum();
        RuleFor(x => x.Fim).IntervaloValido(x => x.Inicio);
    }
}

public sealed class RemarcarCompromissoHandler(IValidator<RemarcarCompromissoCommand> validador, IAgendaRepository agenda,
    IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache,
    ServicoEscala escala, ServicoFechamento fechamento)
{
    public async Task<Guid> Handle(RemarcarCompromissoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        var compromisso = await agenda.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Compromisso");
        var instancias = await agenda.InstanciasDoEscopoAsync(compromisso, comando.Escopo, usuario, ct);
        uow.ExigirVersao(compromisso, comando.Versao);

        var intervalo = IntervaloHorario.Criar(comando.Inicio, comando.Fim);
        var novoId = await uow.EmTransacaoAsync(async tct =>
        {
            // Grupo: todas as travas de fechamento (em ordem) antes de todas as de agenda; com uma instância, o fluxo de sempre.
            foreach (var c in instancias.OrderBy(c => c.ProfissionalId.ToString("N"), StringComparer.Ordinal))
            {
                await fechamento.ExigirSemanaAbertaAsync(c.ProfissionalId, c.Intervalo, tct);
                await fechamento.ExigirSemanaAbertaAsync(c.ProfissionalId, intervalo, tct);
            }
            if (comando.Escopo == EscopoGrupo.Grupo)
                await agenda.GarantirGrupoSemConflitoAsync([.. instancias.Select(c => (c.ProfissionalId, (Guid?)c.Id))], intervalo,
                    compromisso.Tipo.OcupaHorario(), tct);
            else
                await agenda.GarantirSemConflitoAsync(compromisso.ProfissionalId, compromisso.AlunoId, intervalo,
                    compromisso.Id, compromisso.Tipo.OcupaHorario(), tct, listarConflitos: compromisso.Tipo == TipoCompromisso.Bloqueio);
            if (compromisso.Tipo.SegueEscala())
                foreach (var c in instancias) await escala.ExigirDentroDaEscalaAsync(c.ProfissionalId, comando.Inicio, tct);
            var novos = new List<Compromisso>();
            foreach (var c in instancias)
            {
                novos.Add(c.Remarcar(intervalo, usuario.Id));
                agenda.Adicionar(novos[^1]);
                auditoria.Registrar("agenda.remarcacao", "Compromisso", c.Id);
            }
            await uow.SalvarAsync(tct);
            return novos[0].Id;
        }, ct);
        await cache.InvalidarAreaAsync(ct);
        return novoId;
    }
}
