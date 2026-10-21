using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Cursos.Commands;

public record RenomearCursoCommand(Guid Id, string Nome, string Versao);

public class RenomearCursoValidator : AbstractValidator<RenomearCursoCommand>
{
    public RenomearCursoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).Must(n => n?.Trim().Length is >= 2 and <= 120).WithMessage(RegrasCurso.MensagemNome);
    }
}

public sealed class RenomearCursoHandler(IValidator<RenomearCursoCommand> validador, ICursoRepository cursos, IUsuarioAtual usuario,
    IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(RenomearCursoCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(comando, ct);
        var curso = await cursos.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Curso");
        uow.ExigirVersao(curso, comando.Versao);
        if (await cursos.NomeExisteAsync(comando.Nome.Trim(), curso.Id, ct)) throw new ConflictException(RegrasCurso.MensagemNomeRepetido);

        curso.Renomear(comando.Nome);
        auditoria.Registrar("curso.renomear", "Curso", curso.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
