using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Peis.Commands;

public record RegistrarCienciaPeiCommand(Guid VersaoId);

public class RegistrarCienciaPeiValidator : AbstractValidator<RegistrarCienciaPeiCommand>
{
    public RegistrarCienciaPeiValidator() => RuleFor(x => x.VersaoId).NotEmpty();
}

public sealed class RegistrarCienciaPeiHandler(IValidator<RegistrarCienciaPeiCommand> validador, IPeiRepository peis,
    IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public const string MensagemCienciaDuplicada = "Ciência já registrada para esta versão.";

    public async Task Handle(RegistrarCienciaPeiCommand comando, CancellationToken ct = default)
    {
        AutorizacaoPei.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);
        var (_, versao) = await AutorizacaoPei.ObterVersaoAsync(peis, usuario, comando.VersaoId, ct);
        if (!AutorizacaoPei.PodeRegistrarCiencia(usuario, versao)) throw new ForbiddenException();

        // Idempotente: a ciência repetida não grava nem audita de novo.
        var antes = versao.Ciencias.Count;
        versao.RegistrarCiencia(usuario.Id, relogio.Agora);
        if (versao.Ciencias.Count == antes) return;
        auditoria.Registrar("pei.ciencia", "VersaoPei", versao.Id);
        try
        {
            await uow.SalvarAsync(ct);
        }
        catch (ConflictException e) when (e.Message == MensagemCienciaDuplicada)
        {
            // Índice único (versão, coordenador): outra requisição do mesmo coordenador gravou antes, e o resultado é o mesmo.
            // Conflito de versão (rowversion) não entra aqui: precisa chegar ao cliente.
            return;
        }
        await cache.InvalidarAreaAsync(ct);
    }
}
