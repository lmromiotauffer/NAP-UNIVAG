using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Peis.Commands;

public record DevolverPeiCommand(Guid VersaoId, string Justificativa, string Versao);

public class DevolverPeiValidator : AbstractValidator<DevolverPeiCommand>
{
    public DevolverPeiValidator()
    {
        RuleFor(x => x.VersaoId).NotEmpty();
        RuleFor(x => x.Justificativa).Must(t => t?.Trim().Length is >= 3 and <= 500)
            .WithMessage("A justificativa deve ter de 3 a 500 caracteres.");
    }
}

public sealed class DevolverPeiHandler(IValidator<DevolverPeiCommand> validador, IPeiRepository peis,
    IAcessoAssinaturaRepository acessos, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria,
    IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(DevolverPeiCommand comando, CancellationToken ct = default)
    {
        AutorizacaoPei.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);
        var (_, versao) = await AutorizacaoPei.ObterVersaoAsync(peis, usuario, comando.VersaoId, ct);
        if (!AutorizacaoPei.PodeRevisarEPublicar(usuario)) throw new ForbiddenException();
        uow.ExigirVersao(versao, comando.Versao);

        var agora = relogio.Agora;
        versao.Devolver(comando.Justificativa, agora);
        // O conteúdo volta a ser editável: um link já entregue ao aluno não pode assinar outro texto.
        await acessos.InvalidarAbertosDaVersaoAsync(versao.Id, agora, ct);
        auditoria.Registrar("pei.revisao.devolver", "VersaoPei", versao.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
