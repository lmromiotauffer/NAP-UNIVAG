using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Alunos.Commands;

public record AtualizarAlunoCommand(Guid AlunoId, string NomeCompleto, int Semestre, string? NecessidadesEducacionais,
    string? ObservacoesAcademicas, Guid ProfissionalResponsavelId, bool MatriculaAtiva, string Versao);

public class AtualizarAlunoValidator : AbstractValidator<AtualizarAlunoCommand>
{
    public AtualizarAlunoValidator()
    {
        RuleFor(x => x.AlunoId).NotEmpty();
        RuleFor(x => x.NomeCompleto).NotEmpty().WithMessage("Informe o nome completo.").Length(3, 200);
        RuleFor(x => x.Semestre).InclusiveBetween(1, 20);
        RuleFor(x => x.ProfissionalResponsavelId).NotEmpty().WithMessage("Informe o profissional responsável.");
        RuleFor(x => x.NecessidadesEducacionais).MaximumLength(4000);
        RuleFor(x => x.ObservacoesAcademicas).MaximumLength(4000);
    }
}

public sealed class AtualizarAlunoHandler(IValidator<AtualizarAlunoCommand> validador, IAlunoRepository alunos,
    IUsuarioQueries usuarios, IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(AtualizarAlunoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        var aluno = await alunos.ObterAsync(comando.AlunoId, ct) ?? throw new NotFoundException("Aluno");
        uow.ExigirVersao(aluno, comando.Versao);
        // Manter o responsável atual (mesmo inativo) é permitido; trocar exige profissional ativo.
        if (comando.ProfissionalResponsavelId != aluno.ProfissionalResponsavelId
            && !await usuarios.EhProfissionalAtivoAsync(comando.ProfissionalResponsavelId, ct))
            throw Validacao.ErroDeCampo("profissionalResponsavelId", "O responsável deve ser um profissional ativo da equipe NAP.");

        aluno.AtualizarDados(comando.NomeCompleto, comando.Semestre, comando.NecessidadesEducacionais,
            comando.ObservacoesAcademicas, comando.ProfissionalResponsavelId, comando.MatriculaAtiva);
        auditoria.Registrar("aluno.atualizar", "Aluno", aluno.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
