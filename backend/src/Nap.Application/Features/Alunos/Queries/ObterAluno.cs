using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Alunos.Queries;

public record ObterAlunoQuery(Guid AlunoId);

public class ObterAlunoValidator : AbstractValidator<ObterAlunoQuery>
{
    public ObterAlunoValidator() => RuleFor(x => x.AlunoId).NotEmpty();
}

public sealed class ObterAlunoHandler(IValidator<ObterAlunoQuery> validador, IAlunoQueries consultas, IUsuarioAtual usuario,
    IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task<AlunoFichaDto> Handle(ObterAlunoQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        var escopo = EscopoAlunos.Para(usuario);
        var ficha = await consultas.ObterFichaAsync(query.AlunoId, escopo, ct) ?? throw new NotFoundException("Aluno");
        auditoria.Registrar("aluno.consulta", "Aluno", ficha.Id);
        await uow.SalvarAsync(ct);
        return ficha;
    }
}
