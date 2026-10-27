using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Peis.Commands;

public record AtualizarConteudoPeiCommand(Guid VersaoId, ConteudoPeiInput Conteudo, string Versao);

/// <summary>Versão gravada: a próxima edição a usa no If-Match sem reler a versão (uma releitura pegaria a de outra aba).</summary>
public record VersaoAtualizadaDto(string Versao);

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
    public async Task<VersaoAtualizadaDto> Handle(AtualizarConteudoPeiCommand comando, CancellationToken ct = default)
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
        return new VersaoAtualizadaDto(uow.Versao(versao));
    }
}
