using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Configuracoes.Commands;

public record AtualizarConfiguracaoAlertasCommand(int FaltasConsecutivas, int FaltasJanela, int JanelaDias,
    int DiasSemAcolhimento, int DiasSemAtualizacao, int DiasPeiPendente, string Versao);

// As faixas repetem as do domínio para devolver o erro no campo; o domínio continua sendo a última barreira.
public class AtualizarConfiguracaoAlertasValidator : AbstractValidator<AtualizarConfiguracaoAlertasCommand>
{
    public AtualizarConfiguracaoAlertasValidator()
    {
        RuleFor(x => x.FaltasConsecutivas).InclusiveBetween(1, 20).WithMessage("Faltas consecutivas deve estar entre 1 e 20.");
        RuleFor(x => x.FaltasJanela).InclusiveBetween(1, 50).WithMessage("Faltas na janela deve estar entre 1 e 50.");
        RuleFor(x => x.JanelaDias).InclusiveBetween(7, 365).WithMessage("Janela em dias deve estar entre 7 e 365.");
        RuleFor(x => x.DiasSemAcolhimento).InclusiveBetween(1, 90).WithMessage("Dias sem acolhimento deve estar entre 1 e 90.");
        RuleFor(x => x.DiasSemAtualizacao).InclusiveBetween(1, 180).WithMessage("Dias sem atualização deve estar entre 1 e 180.");
        RuleFor(x => x.DiasPeiPendente).InclusiveBetween(1, 180).WithMessage("Dias de PEI pendente deve estar entre 1 e 180.");
    }
}

public sealed class AtualizarConfiguracaoAlertasHandler(IValidator<AtualizarConfiguracaoAlertasCommand> validador,
    IConfiguracaoAlertas configuracao, IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task<ConfiguracaoAlertasDto> Handle(AtualizarConfiguracaoAlertasCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(comando, ct);

        var linha = await configuracao.ObterParaEdicaoAsync(ct);
        uow.ExigirVersao(linha, comando.Versao);
        linha.Atualizar(comando.FaltasConsecutivas, comando.FaltasJanela, comando.JanelaDias, comando.DiasSemAcolhimento,
            comando.DiasSemAtualizacao, comando.DiasPeiPendente);
        auditoria.Registrar("configuracao.alertas", "ConfiguracaoAlertas", linha.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
        return await configuracao.ObterDtoAsync(ct);
    }
}
