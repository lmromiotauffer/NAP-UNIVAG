using FluentValidation;
using Nap.Application.Comum;
using Nap.Domain.Alunos;

namespace Nap.Application.Features.Alunos.Commands;

public record AlterarSituacaoAlunoCommand(Guid AlunoId, SituacaoAluno Situacao, string Motivo, string Versao);

public class AlterarSituacaoAlunoValidator : AbstractValidator<AlterarSituacaoAlunoCommand>
{
    public AlterarSituacaoAlunoValidator()
    {
        RuleFor(x => x.AlunoId).NotEmpty();
        RuleFor(x => x.Situacao).IsInEnum();
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("Informe o motivo.").Length(3, 500);
    }
}

public sealed class AlterarSituacaoAlunoHandler(IValidator<AlterarSituacaoAlunoCommand> validador, IAlunoRepository alunos,
    IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(AlterarSituacaoAlunoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        var aluno = await alunos.ObterAsync(comando.AlunoId, ct) ?? throw new NotFoundException("Aluno");
        uow.ExigirVersao(aluno, comando.Versao);
        aluno.AlterarSituacao(comando.Situacao, comando.Motivo, usuario.Id, relogio.Agora);
        auditoria.Registrar("aluno.situacao", "Aluno", aluno.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
