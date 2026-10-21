using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Agenda.Commands;

public record AtualizarCompromissoCommand(Guid Id, string Titulo, string? Descricao, string Versao, EscopoGrupo Escopo = EscopoGrupo.Minha);

public class AtualizarCompromissoValidator : AbstractValidator<AtualizarCompromissoCommand>
{
    public AtualizarCompromissoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Escopo).IsInEnum();
        RuleFor(x => x.Titulo).NotEmpty().WithMessage("Informe o título.").MaximumLength(120);
        RuleFor(x => x.Descricao).MaximumLength(2000);
    }
}

public sealed class AtualizarCompromissoHandler(IValidator<AtualizarCompromissoCommand> validador, IAgendaRepository agenda,
    IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(AtualizarCompromissoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        var compromisso = await agenda.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Compromisso");
        var instancias = await agenda.InstanciasDoEscopoAsync(compromisso, comando.Escopo, usuario, ct);
        uow.ExigirVersao(compromisso, comando.Versao);

        if (comando.Escopo == EscopoGrupo.Minha)
        {
            compromisso.Atualizar(comando.Titulo, comando.Descricao);
            auditoria.Registrar("agenda.atualizacao", "Compromisso", compromisso.Id);
            await uow.SalvarAsync(ct);
        }
        else
            await uow.EmTransacaoAsync(async tct =>
            {
                await agenda.BloquearAgendasAsync([.. instancias.Select(c => c.ProfissionalId)], tct);
                foreach (var c in instancias)
                {
                    c.Atualizar(comando.Titulo, comando.Descricao);
                    auditoria.Registrar("agenda.atualizacao", "Compromisso", c.Id);
                }
                await uow.SalvarAsync(tct);
                return compromisso.Id;
            }, ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
