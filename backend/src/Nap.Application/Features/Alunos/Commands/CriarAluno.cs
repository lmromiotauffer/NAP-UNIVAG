using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Cursos;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Alunos;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Features.Alunos.Commands;

public record CriarAlunoCommand(string NomeCompleto, string Matricula, Guid CursoId, int Semestre,
    DateOnly InicioAcompanhamento, Guid ProfissionalResponsavelId, string? NecessidadesEducacionais,
    string? ObservacoesAcademicas);

public class CriarAlunoValidator : AbstractValidator<CriarAlunoCommand>
{
    public CriarAlunoValidator()
    {
        RuleFor(x => x.NomeCompleto).NotEmpty().WithMessage("Informe o nome completo.").Length(3, 200);
        RuleFor(x => x.Matricula).NotEmpty().WithMessage("Informe a matrícula.")
            .Must(m => string.IsNullOrWhiteSpace(m) || System.Text.RegularExpressions.Regex.IsMatch(m.Trim(), "^[A-Za-z0-9.-]{3,20}$"))
            .WithMessage("A matrícula deve ter de 3 a 20 caracteres (letras, números, ponto ou hífen).");
        RuleFor(x => x.CursoId).NotEmpty().WithMessage("Informe o curso.");
        RuleFor(x => x.Semestre).InclusiveBetween(1, 20);
        RuleFor(x => x.InicioAcompanhamento).NotEqual(default(DateOnly)).WithMessage("Informe o início do acompanhamento.");
        RuleFor(x => x.ProfissionalResponsavelId).NotEmpty().WithMessage("Informe o profissional responsável.");
        RuleFor(x => x.NecessidadesEducacionais).MaximumLength(4000);
        RuleFor(x => x.ObservacoesAcademicas).MaximumLength(4000);
    }
}

public sealed class CriarAlunoHandler(IValidator<CriarAlunoCommand> validador, IAlunoRepository alunos,
    IUsuarioQueries usuarios, ICursoQueries cursos, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task<Guid> Handle(CriarAlunoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();
        if (comando.InicioAcompanhamento > relogio.Hoje)
            throw Validacao.ErroDeCampo("inicioAcompanhamento", "O início do acompanhamento não pode ser uma data futura.");

        var matricula = Matricula.Criar(comando.Matricula);
        if (await alunos.MatriculaExisteAsync(matricula.Valor, ct))
            throw new ConflictException("Matrícula já cadastrada");
        if (!await cursos.CursoAtivoExisteAsync(comando.CursoId, ct))
            throw Validacao.ErroDeCampo("cursoId", "Curso inexistente ou inativo.");
        if (!await usuarios.EhProfissionalAtivoAsync(comando.ProfissionalResponsavelId, ct))
            throw Validacao.ErroDeCampo("profissionalResponsavelId", "O responsável deve ser um profissional ativo da equipe NAP.");

        var aluno = Aluno.Criar(comando.NomeCompleto, matricula, comando.CursoId, comando.Semestre,
            comando.InicioAcompanhamento, comando.ProfissionalResponsavelId, comando.NecessidadesEducacionais,
            comando.ObservacoesAcademicas, usuario.Id, relogio.Agora);
        alunos.Adicionar(aluno);
        auditoria.Registrar("aluno.criar", "Aluno", aluno.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
        return aluno.Id;
    }
}
