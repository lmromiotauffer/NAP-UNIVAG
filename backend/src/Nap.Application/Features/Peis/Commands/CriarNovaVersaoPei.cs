using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Peis.Commands;

public record CriarNovaVersaoPeiCommand(Guid PeiId);

public class CriarNovaVersaoPeiValidator : AbstractValidator<CriarNovaVersaoPeiCommand>
{
    public CriarNovaVersaoPeiValidator() => RuleFor(x => x.PeiId).NotEmpty();
}

public sealed class CriarNovaVersaoPeiHandler(IValidator<CriarNovaVersaoPeiCommand> validador, IPeiRepository peis,
    IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    /// <summary>Mesma mensagem da violação do índice de versão aberta (corrida entre duas criações).</summary>
    public const string MensagemVersaoAberta = "Já existe uma versão em elaboração.";

    public const string MensagemVersaoAssinada = "Há uma versão assinada aguardando publicação; publique-a antes de criar outra.";

    public async Task<Guid> Handle(CriarNovaVersaoPeiCommand comando, CancellationToken ct = default)
    {
        AutorizacaoPei.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);
        // Só a Equipe NAP vê o PEI como um todo (o coordenador de curso vê versões publicadas).
        if (!AutorizacaoPei.PodeElaborar(usuario)) throw new ForbiddenException();
        var pei = await peis.ObterAsync(comando.PeiId, ct) ?? throw new NotFoundException("PEI");
        if (pei.VersaoAberta is not null) throw new ConflictException(MensagemVersaoAberta);
        if (pei.ImpedeNovaVersao) throw new ConflictException(MensagemVersaoAssinada);

        var versao = pei.NovaVersao(usuario.Id, relogio.Agora);
        auditoria.Registrar("pei.versao.criar", "VersaoPei", versao.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
        return versao.Id;
    }
}
