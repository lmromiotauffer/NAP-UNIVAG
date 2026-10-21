using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Cursos;
using Nap.Domain.Comum;

namespace Nap.Application.Features.Usuarios.Commands;

public record AtualizarUsuarioCommand(Guid Id, string NomeCompleto, string Perfil, Guid? CursoId, string Versao);

public class AtualizarUsuarioValidator : AbstractValidator<AtualizarUsuarioCommand>
{
    public AtualizarUsuarioValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NomeCompleto).NotEmpty().WithMessage("Informe o nome completo.")
            .Must(n => n is null || n.Trim().Length is >= 3 and <= 200).WithMessage("O nome deve ter de 3 a 200 caracteres.");
        RuleFor(x => x.Perfil).Must(p => p is not null && Perfis.Todos.Contains(p)).WithMessage("Perfil inválido.");
        RuleFor(x => x.CursoId).Must((x, c) => RegrasAlcada.CursoCoerente(x.Perfil, c)).WithMessage(RegrasAlcada.MensagemCurso);
        RuleFor(x => x.Versao).NotEmpty();
    }
}

public sealed class AtualizarUsuarioHandler(IValidator<AtualizarUsuarioCommand> validador, IGestaoUsuarios gestao, ICursoQueries cursos,
    IUsuarioAtual usuario, IAuditoria auditoria, ICacheConsultas cache)
{
    public async Task Handle(AtualizarUsuarioCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(comando, ct);
        var alvo = await gestao.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Usuário");
        RegrasAlcada.ExigirGerenciar(usuario, alvo.Perfil);
        RegrasAlcada.ExigirGerenciar(usuario, comando.Perfil);

        var perfilMudou = comando.Perfil != alvo.Perfil;
        // Do próprio cadastro, só o nome pode ser editado.
        if (perfilMudou || comando.CursoId != alvo.CursoId)
            RegrasAlcada.ExigirNaoProprio(usuario, alvo.Id);
        if (perfilMudou)
            await RegrasAlcada.ExigirOutroAdministradorAsync(gestao, alvo, ct);
        // Curso inativo não pode ser atribuído, mas quem já coordena um curso inativado continua editável.
        if (comando.CursoId is { } cursoId && cursoId != alvo.CursoId && !await cursos.CursoAtivoExisteAsync(cursoId, ct))
            throw Validacao.ErroDeCampo("cursoId", "Curso inexistente ou inativo.");

        auditoria.Registrar("usuario.atualizar", "Usuario", alvo.Id);
        await gestao.AtualizarAsync(alvo.Id, comando.NomeCompleto.Trim(), comando.Perfil, comando.CursoId, comando.Versao, ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
