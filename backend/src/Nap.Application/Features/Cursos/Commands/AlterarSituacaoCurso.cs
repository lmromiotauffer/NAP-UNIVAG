using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Cursos.Commands;

/// <summary>Inativar com alunos ou coordenadores ativos é permitido: os vínculos existentes ficam, só novas atribuições param.</summary>
public record AlterarSituacaoCursoCommand(Guid Id, bool Ativo, string Versao);

public class AlterarSituacaoCursoValidator : AbstractValidator<AlterarSituacaoCursoCommand>
{
    public AlterarSituacaoCursoValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class AlterarSituacaoCursoHandler(IValidator<AlterarSituacaoCursoCommand> validador, ICursoRepository cursos,
    IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(AlterarSituacaoCursoCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(comando, ct);
        var curso = await cursos.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Curso");
        uow.ExigirVersao(curso, comando.Versao);
        if (curso.Ativo == comando.Ativo) return;

        if (comando.Ativo) curso.Reativar(); else curso.Inativar();
        auditoria.Registrar(comando.Ativo ? "curso.reativar" : "curso.inativar", "Curso", curso.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
