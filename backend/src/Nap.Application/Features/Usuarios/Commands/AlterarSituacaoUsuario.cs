using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Usuarios.Commands;

public record AlterarSituacaoUsuarioCommand(Guid Id, bool Ativo, string Versao);

public class AlterarSituacaoUsuarioValidator : AbstractValidator<AlterarSituacaoUsuarioCommand>
{
    public AlterarSituacaoUsuarioValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Versao).NotEmpty();
    }
}

public sealed class AlterarSituacaoUsuarioHandler(IValidator<AlterarSituacaoUsuarioCommand> validador, IGestaoUsuarios gestao,
    IUsuarioAtual usuario, IAuditoria auditoria, ICacheConsultas cache)
{
    public async Task Handle(AlterarSituacaoUsuarioCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(comando, ct);
        var alvo = await gestao.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Usuário");
        RegrasAlcada.ExigirGerenciar(usuario, alvo.Perfil);
        RegrasAlcada.ExigirNaoProprio(usuario, alvo.Id);
        if (!comando.Ativo)
            await RegrasAlcada.ExigirOutroAdministradorAsync(gestao, alvo, ct);

        auditoria.Registrar("usuario.situacao", "Usuario", alvo.Id);
        await gestao.AlterarSituacaoAsync(alvo.Id, comando.Ativo, comando.Versao, ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
