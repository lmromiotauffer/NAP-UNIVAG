using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Cursos;

namespace Nap.Application.Features.Alunos.Commands;

public record TransferirCursoAlunoCommand(Guid AlunoId, Guid NovoCursoId, DateOnly Data, string Motivo, string Versao);

public class TransferirCursoAlunoValidator : AbstractValidator<TransferirCursoAlunoCommand>
{
    public TransferirCursoAlunoValidator()
    {
        RuleFor(x => x.AlunoId).NotEmpty();
        RuleFor(x => x.NovoCursoId).NotEmpty().WithMessage("Informe o novo curso.");
        RuleFor(x => x.Data).NotEqual(default(DateOnly)).WithMessage("Informe a data da transferência.");
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("Informe o motivo.").Length(3, 500);
    }
}

public sealed class TransferirCursoAlunoHandler(IValidator<TransferirCursoAlunoCommand> validador, IAlunoRepository alunos,
    ICursoQueries cursos, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(TransferirCursoAlunoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();
        if (comando.Data > relogio.Hoje)
            throw Validacao.ErroDeCampo("data", "A data da transferência não pode ser futura.");

        var aluno = await alunos.ObterAsync(comando.AlunoId, ct) ?? throw new NotFoundException("Aluno");
        uow.ExigirVersao(aluno, comando.Versao);
        if (!await cursos.CursoAtivoExisteAsync(comando.NovoCursoId, ct))
            throw Validacao.ErroDeCampo("novoCursoId", "Curso inexistente ou inativo.");

        aluno.TransferirCurso(comando.NovoCursoId, comando.Data, comando.Motivo, usuario.Id, relogio.Agora);
        auditoria.Registrar("aluno.transferir", "Aluno", aluno.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
