using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Peis.Commands;

public record AtualizarConteudoPeiCommand(Guid VersaoId, ConteudoPeiInput Conteudo, string Versao);

public class AtualizarConteudoPeiValidator : AbstractValidator<AtualizarConteudoPeiCommand>
{
    public AtualizarConteudoPeiValidator()
    {
        RuleFor(x => x.VersaoId).NotEmpty();
        RuleFor(x => x.Conteudo).NotNull().WithMessage("Informe o conteúdo do PEI.");
    }
}

public sealed class AtualizarConteudoPeiHandler(IValidator<AtualizarConteudoPeiCommand> validador, IPeiRepository peis,
    IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(AtualizarConteudoPeiCommand comando, CancellationToken ct = default)
    {
        AutorizacaoPei.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);
        var (_, versao) = await AutorizacaoPei.ObterVersaoAsync(peis, usuario, comando.VersaoId, ct);
        if (!AutorizacaoPei.PodeElaborar(usuario)) throw new ForbiddenException();
        uow.ExigirVersao(versao, comando.Versao);

        versao.AtualizarConteudo(comando.Conteudo.ParaDominio(), relogio.Agora);
        auditoria.Registrar("pei.atualizar", "VersaoPei", versao.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
