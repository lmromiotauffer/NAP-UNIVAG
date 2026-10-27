using FluentValidation;
using Nap.Application.Comum;
using Nap.Domain.Comum;
using Nap.Domain.Peis;

namespace Nap.Application.Features.Peis.Commands;

/// <summary>Token e código só aparecem nesta resposta; nunca vão para log nem auditoria.</summary>
public record AcessoGeradoDto(Guid AcessoId, string Token, string Codigo, DateTimeOffset ExpiraEm);

public record GerarAcessoAssinaturaAlunoCommand(Guid VersaoId);

public class GerarAcessoAssinaturaAlunoValidator : AbstractValidator<GerarAcessoAssinaturaAlunoCommand>
{
    public GerarAcessoAssinaturaAlunoValidator() => RuleFor(x => x.VersaoId).NotEmpty();
}

public sealed class GerarAcessoAssinaturaAlunoHandler(IValidator<GerarAcessoAssinaturaAlunoCommand> validador,
    IPeiRepository peis, IAcessoAssinaturaRepository acessos, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria,
    IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task<AcessoGeradoDto> Handle(GerarAcessoAssinaturaAlunoCommand comando, CancellationToken ct = default)
    {
        AutorizacaoPei.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);
        var (_, versao) = await AutorizacaoPei.ObterVersaoAsync(peis, usuario, comando.VersaoId, ct);
        if (!AutorizacaoPei.PodeElaborar(usuario)) throw new ForbiddenException();
        if (versao.Estado != EstadoVersaoPei.AguardandoAssinaturas || versao.HashConteudo is not { } hash
            || versao.AssinaturaValida(PapelAssinatura.Aluno) is not null)
            throw new DomainException("O acesso do aluno só pode ser gerado em versão aguardando a assinatura dele.");

        var agora = relogio.Agora;
        await acessos.InvalidarAbertosDaVersaoAsync(versao.Id, agora, ct);
        var (acesso, token, codigo) = AcessoAssinaturaPei.Gerar(versao.Id, hash, usuario.Id, agora);
        acessos.Adicionar(acesso);
        auditoria.Registrar("pei.acesso.gerar", "AcessoAssinaturaPei", acesso.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
        return new AcessoGeradoDto(acesso.Id, token, codigo, acesso.ExpiraEm);
    }
}
