using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Agenda;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Features.Agenda.Commands;

public record CriarAutorizacaoForaEscalaCommand(Guid ProfissionalId, DateTimeOffset Inicio, DateTimeOffset Fim, string Justificativa);

public class CriarAutorizacaoForaEscalaValidator : AbstractValidator<CriarAutorizacaoForaEscalaCommand>
{
    public CriarAutorizacaoForaEscalaValidator()
    {
        RuleFor(x => x.ProfissionalId).NotEmpty().WithMessage("Informe o profissional.");
        RuleFor(x => x.Justificativa).Must(j => j?.Trim().Length is >= 5 and <= 500)
            .WithMessage("A justificativa da autorização é obrigatória e deve ter de 5 a 500 caracteres.");
    }
}

/// <summary>Libera o registro de atendimento fora da escala (ou em feriado) para o profissional, num intervalo de até 12 h.</summary>
public sealed class CriarAutorizacaoForaEscalaHandler(IValidator<CriarAutorizacaoForaEscalaCommand> validador, IEscalaRepository escalas,
    IUsuarioQueries usuarios, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task<Guid> Handle(CriarAutorizacaoForaEscalaCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(comando, ct);
        if (!await usuarios.EhProfissionalAtivoAsync(comando.ProfissionalId, ct))
            throw Validacao.ErroDeCampo("profissionalId", "O profissional deve ser um membro ativo da equipe NAP.");

        var autorizacao = AutorizacaoForaEscala.Criar(comando.ProfissionalId, IntervaloHorario.Criar(comando.Inicio, comando.Fim),
            comando.Justificativa, usuario.Id, relogio.Agora);
        escalas.AdicionarAutorizacao(autorizacao);
        auditoria.Registrar("escala.autorizacao.criar", "AutorizacaoForaEscala", autorizacao.Id);
        await uow.SalvarAsync(ct);
        return autorizacao.Id;
    }
}
