using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comum;
using Nap.Domain.Peis;

namespace Nap.Application.Features.Peis.Commands;

public record AssinarPeiProfissionalCommand(Guid VersaoId, string Versao);

public class AssinarPeiProfissionalValidator : AbstractValidator<AssinarPeiProfissionalCommand>
{
    public AssinarPeiProfissionalValidator() => RuleFor(x => x.VersaoId).NotEmpty();
}

public sealed class AssinarPeiProfissionalHandler(IValidator<AssinarPeiProfissionalCommand> validador, IPeiRepository peis,
    IServicoAssinatura assinaturas, IUsuarioQueries usuarios, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria,
    IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(AssinarPeiProfissionalCommand comando, CancellationToken ct = default)
    {
        AutorizacaoPei.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);
        var (pei, versao) = await AutorizacaoPei.ObterVersaoAsync(peis, usuario, comando.VersaoId, ct);
        if (!AutorizacaoPei.PodeAssinarComoProfissional(usuario, pei)) throw new ForbiddenException();
        uow.ExigirVersao(versao, comando.Versao);

        // Antes de pedir a evidência: sem hash congelado não há o que assinar.
        if (versao.Estado != EstadoVersaoPei.AguardandoAssinaturas || versao.HashConteudo is not { } hash)
            throw new DomainException("A versão não está aguardando assinaturas.");
        var nome = (await usuarios.ObterResumoAsync(usuario.Id, ct))?.Nome ?? throw new ForbiddenException();
        var agora = relogio.Agora;
        var evidencia = await assinaturas.AssinarAsync(versao.Id, PapelAssinatura.Profissional, nome, hash, agora, ct);

        versao.RegistrarAssinatura(PapelAssinatura.Profissional, usuario.Id, nome, hash, evidencia.Evidencia, agora);
        // A auditoria só guarda ids: o papel fica na assinatura referenciada.
        var assinatura = versao.Assinaturas.Single(a => a.Papel == PapelAssinatura.Profissional);
        auditoria.Registrar("pei.assinar", "AssinaturaPei", assinatura.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
