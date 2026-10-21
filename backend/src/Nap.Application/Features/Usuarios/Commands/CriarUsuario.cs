using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Cursos;
using Nap.Domain.Comum;

namespace Nap.Application.Features.Usuarios.Commands;

public record CriarUsuarioCommand(string Email, string NomeCompleto, string Perfil, Guid? CursoId);

public class CriarUsuarioValidator : AbstractValidator<CriarUsuarioCommand>
{
    public CriarUsuarioValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Informe o e-mail.").MaximumLength(256)
            .EmailAddress().WithMessage("E-mail inválido.");
        RuleFor(x => x.NomeCompleto).NotEmpty().WithMessage("Informe o nome completo.")
            .Must(n => n is null || n.Trim().Length is >= 3 and <= 200).WithMessage("O nome deve ter de 3 a 200 caracteres.");
        RuleFor(x => x.Perfil).Must(p => p is not null && Perfis.Todos.Contains(p)).WithMessage("Perfil inválido.");
        RuleFor(x => x.CursoId).Must((x, c) => RegrasAlcada.CursoCoerente(x.Perfil, c)).WithMessage(RegrasAlcada.MensagemCurso);
    }
}

public sealed class CriarUsuarioHandler(IValidator<CriarUsuarioCommand> validador, IGestaoUsuarios gestao, ICursoQueries cursos,
    IUsuarioAtual usuario, IAuditoria auditoria, ICacheConsultas cache)
{
    public async Task<SenhaTemporariaDto> Handle(CriarUsuarioCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(comando, ct);
        RegrasAlcada.ExigirGerenciar(usuario, comando.Perfil);
        if (comando.CursoId is { } cursoId && !await cursos.CursoAtivoExisteAsync(cursoId, ct))
            throw Validacao.ErroDeCampo("cursoId", "Curso inexistente ou inativo.");

        var novo = new NovoUsuario(Guid.CreateVersion7(), comando.Email.Trim(), comando.NomeCompleto.Trim(), comando.Perfil, comando.CursoId);
        var senha = GeradorSenhaTemporaria.Gerar();
        auditoria.Registrar("usuario.criar", "Usuario", novo.Id);
        await gestao.CriarAsync(novo, senha, ct);
        await cache.InvalidarAreaAsync(ct);
        return new SenhaTemporariaDto(novo.Id, senha);
    }
}
