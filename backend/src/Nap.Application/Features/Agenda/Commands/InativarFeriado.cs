using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Agenda.Commands;

public record InativarFeriadoCommand(Guid Id);

public sealed class InativarFeriadoHandler(IFeriadoRepository feriados, IUsuarioAtual usuario, IAuditoria auditoria,
    IUnidadeDeTrabalho uow)
{
    public async Task Handle(InativarFeriadoCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        var feriado = await feriados.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Feriado");
        if (!feriado.Ativo) return;

        feriado.Inativar();
        auditoria.Registrar("feriado.inativar", "Feriado", feriado.Id);
        await uow.SalvarAsync(ct);
    }
}
