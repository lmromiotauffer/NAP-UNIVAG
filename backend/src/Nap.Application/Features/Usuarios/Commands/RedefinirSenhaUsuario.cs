using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Usuarios.Commands;

public record RedefinirSenhaUsuarioCommand(Guid Id);

public class RedefinirSenhaUsuarioValidator : AbstractValidator<RedefinirSenhaUsuarioCommand>
{
    public RedefinirSenhaUsuarioValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class RedefinirSenhaUsuarioHandler(IValidator<RedefinirSenhaUsuarioCommand> validador, IGestaoUsuarios gestao,
    IUsuarioAtual usuario, IAuditoria auditoria)
{
    public async Task<SenhaTemporariaDto> Handle(RedefinirSenhaUsuarioCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(comando, ct);
        var alvo = await gestao.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Usuário");
        RegrasAlcada.ExigirGerenciar(usuario, alvo.Perfil);
        // A própria senha se troca pela troca de senha, não pela redefinição.
        RegrasAlcada.ExigirNaoProprio(usuario, alvo.Id);

        var senha = GeradorSenhaTemporaria.Gerar();
        auditoria.Registrar("usuario.senha.redefinir", "Usuario", alvo.Id);
        // Versão lida: se o alvo mudou (ex.: virou administrador) depois da checagem de alçada, a porta responde 409.
        await gestao.RedefinirSenhaAsync(alvo.Id, senha, alvo.Versao, ct);
        return new SenhaTemporariaDto(alvo.Id, senha);
    }
}
