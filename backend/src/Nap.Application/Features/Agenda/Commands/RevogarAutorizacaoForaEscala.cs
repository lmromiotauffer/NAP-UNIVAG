using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Agenda.Commands;

public record RevogarAutorizacaoForaEscalaCommand(Guid Id);

/// <summary>Registros já feitos sob a autorização permanecem. Trava a agenda do profissional para serializar com registros em andamento.</summary>
public sealed class RevogarAutorizacaoForaEscalaHandler(IEscalaRepository escalas, IAgendaRepository agenda, IUsuarioAtual usuario,
    IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task Handle(RevogarAutorizacaoForaEscalaCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        var autorizacao = await escalas.ObterAutorizacaoAsync(comando.Id, ct) ?? throw new NotFoundException("Autorização");
        await uow.EmTransacaoAsync(async tct =>
        {
            await agenda.BloquearAgendasAsync(autorizacao.ProfissionalId, null, tct);
            autorizacao.Revogar(relogio.Agora);
            auditoria.Registrar("escala.autorizacao.revogar", "AutorizacaoForaEscala", autorizacao.Id);
            await uow.SalvarAsync(tct);
            return autorizacao.Id;
        }, ct);
    }
}
