using Nap.Domain.Alunos;

namespace Nap.Api.Contratos;

public record AtualizarAlunoRequest(string NomeCompleto, int Semestre, string? NecessidadesEducacionais,
    string? ObservacoesAcademicas, Guid ProfissionalResponsavelId);

public record AlterarSituacaoAlunoRequest(SituacaoAluno Situacao, string Motivo);

public record TransferirCursoAlunoRequest(Guid NovoCursoId, DateOnly Data, string Motivo);

public record CriarAlunoRequest(string NomeCompleto, string Matricula, Guid CursoId, int Semestre,
    DateOnly InicioAcompanhamento, Guid ProfissionalResponsavelId, string? NecessidadesEducacionais,
    string? ObservacoesAcademicas);
