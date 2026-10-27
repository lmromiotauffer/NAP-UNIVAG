using Nap.Application.Comum;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Agenda.Commands;

public record InativarFeriadoCommand(Guid Id);

public sealed class InativarFeriadoHandler(IFeriadoRepository feriados, IUsuarioAtual usuario, IAuditoria auditoria,
    IUnidadeDeTrabalho uow, ServicoFechamento fechamento)
{
    public async Task Handle(InativarFeriadoCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        var feriado = await feriados.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Feriado");
        if (!feriado.Ativo) return;

        // shortcut: sem trava (como em CriarFeriado), um fechamento simultâneo escapa da checagem e a capacidade da semana
        // fechada muda; travar por período se fechamentos e cadastro de feriados passarem a coincidir.
        await fechamento.ExigirPeriodoAbertoAsync(null, feriado.Data, feriado.Data, ct);
        feriado.Inativar();
        auditoria.Registrar("feriado.inativar", "Feriado", feriado.Id);
        await uow.SalvarAsync(ct);
    }
}
