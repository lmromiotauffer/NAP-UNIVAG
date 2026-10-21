using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comum;
using Nap.Domain.Producao;

namespace Nap.Application.Features.Producao.Commands;

public record FecharSemanaCommand(Guid ProfissionalId, DateOnly Semana, bool ConfirmarPendencias);

public class FecharSemanaValidator : AbstractValidator<FecharSemanaCommand>
{
    public FecharSemanaValidator()
    {
        RuleFor(x => x.ProfissionalId).NotEmpty();
        RuleFor(x => x.Semana).Must(s => s.DayOfWeek == DayOfWeek.Monday).WithMessage("A semana deve ser identificada pela segunda-feira.");
    }
}

public sealed class FecharSemanaHandler(IValidator<FecharSemanaCommand> validador, IFechamentoRepository fechamentos,
    IProducaoQueries consultas, IUsuarioQueries usuarios, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria,
    IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(FecharSemanaCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();
        if (comando.ProfissionalId != usuario.Id)
        {
            if (!usuario.EhCoordenacao()) throw new ForbiddenException();
            // Inativo também: a lista de semanas mostra quem teve produção e ainda precisa ser fechado.
            if (await usuarios.ObterResumoAsync(comando.ProfissionalId, ct) is not { } alvo || !Perfis.EquipeNap.Contains(alvo.Perfil))
                throw new NotFoundException("Profissional");
        }

        await uow.EmTransacaoAsync(async tct =>
        {
            await fechamentos.BloquearAsync(comando.ProfissionalId, true, tct);
            var existente = await fechamentos.ObterAsync(comando.ProfissionalId, comando.Semana, tct);
            var fechamento = existente ?? FechamentoSemanal.Criar(comando.ProfissionalId, comando.Semana);
            // Primeiro a regra de domínio (semana encerrada); só então a contagem de pendências.
            fechamento.Fechar(usuario.Id, relogio.Agora, relogio.Hoje);
            var pendentes = await consultas.ContarPendentesAsync(comando.ProfissionalId, comando.Semana, tct);
            if (pendentes > 0 && !comando.ConfirmarPendencias)
                throw new DomainException($"A semana tem {pendentes} pendências de classificação.");
            if (existente is null) fechamentos.Adicionar(fechamento);
            auditoria.Registrar("producao.semana.fechar", "FechamentoSemanal", fechamento.Id);
            await uow.SalvarAsync(tct);
            return fechamento.Id;
        }, ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
