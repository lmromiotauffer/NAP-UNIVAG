using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Agenda;

namespace Nap.Application.Features.Agenda.Commands;

public record CriarFeriadoCommand(DateOnly Data, string Descricao);

public record FeriadoCriadoDto(Guid Id, IReadOnlyList<ConflitoFeriadoDto> Conflitos);

public class CriarFeriadoValidator : AbstractValidator<CriarFeriadoCommand>
{
    public CriarFeriadoValidator()
    {
        RuleFor(x => x.Data).NotEqual(default(DateOnly)).WithMessage("Informe a data.");
        RuleFor(x => x.Descricao).Must(d => d?.Trim().Length is >= 3 and <= 120)
            .WithMessage("A descrição do feriado deve ter de 3 a 120 caracteres.");
    }
}

/// <summary>Só cadastra: o que já ocupa a data volta em <c>Conflitos</c> para a coordenação decidir; nada é alterado.</summary>
public sealed class CriarFeriadoHandler(IValidator<CriarFeriadoCommand> validador, IFeriadoRepository feriados,
    IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public const string MensagemDuplicado = "Já existe um feriado nesta data.";

    public async Task<FeriadoCriadoDto> Handle(CriarFeriadoCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(comando, ct);
        if (await feriados.ObterAtivoAsync(comando.Data, ct) is not null)
            throw new ConflictException(MensagemDuplicado);

        // ponytail: criar feriado não toma os locks da agenda; um compromisso confirmado em paralelo pode faltar na
        // lista de conflitos devolvida. Nada é apagado e a agenda segue destacando o feriado.
        // Evolução: BloquearAgendasAsync para todos os profissionais.
        var feriado = Feriado.Criar(comando.Data, comando.Descricao);
        return await uow.EmTransacaoAsync(async tct =>
        {
            feriados.Adicionar(feriado);
            auditoria.Registrar("feriado.criar", "Feriado", feriado.Id);
            await uow.SalvarAsync(tct);
            return new FeriadoCriadoDto(feriado.Id, await feriados.ListarConflitosAsync(feriado.Data, tct));
        }, ct);
    }
}
