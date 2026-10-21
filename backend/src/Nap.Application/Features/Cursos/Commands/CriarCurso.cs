using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Cursos;

namespace Nap.Application.Features.Cursos.Commands;

public record CriarCursoCommand(string Nome);

public class CriarCursoValidator : AbstractValidator<CriarCursoCommand>
{
    public CriarCursoValidator() =>
        RuleFor(x => x.Nome).Must(n => n?.Trim().Length is >= 2 and <= 120).WithMessage(RegrasCurso.MensagemNome);
}

public sealed class CriarCursoHandler(IValidator<CriarCursoCommand> validador, ICursoRepository cursos, IUsuarioAtual usuario,
    IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task<Guid> Handle(CriarCursoCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(comando, ct);
        var curso = Curso.Criar(comando.Nome);
        if (await cursos.NomeExisteAsync(curso.Nome, null, ct)) throw new ConflictException(RegrasCurso.MensagemNomeRepetido);

        cursos.Adicionar(curso);
        auditoria.Registrar("curso.criar", "Curso", curso.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
        return curso.Id;
    }
}

public static class RegrasCurso
{
    public const string MensagemNome = "O nome do curso deve ter de 2 a 120 caracteres.";
    public const string MensagemNomeRepetido = "Já existe um curso com este nome.";
}
