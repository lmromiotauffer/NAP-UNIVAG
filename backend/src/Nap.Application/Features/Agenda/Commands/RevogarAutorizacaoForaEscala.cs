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
        var profissionalId = await escalas.ProfissionalDaAutorizacaoAsync(comando.Id, ct) ?? throw new NotFoundException("Autorização");
        await uow.EmTransacaoAsync(async tct =>
        {
            await agenda.BloquearAgendasAsync(profissionalId, null, tct);
            // Lida só depois da trava: duas revogações simultâneas não passam ambas pela checagem de "já revogada".
            var autorizacao = await escalas.ObterAutorizacaoAsync(comando.Id, tct) ?? throw new NotFoundException("Autorização");
            autorizacao.Revogar(relogio.Agora);
            auditoria.Registrar("escala.autorizacao.revogar", "AutorizacaoForaEscala", autorizacao.Id);
            await uow.SalvarAsync(tct);
            return autorizacao.Id;
        }, ct);
    }
}
