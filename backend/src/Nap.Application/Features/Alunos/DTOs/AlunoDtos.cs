using Nap.Domain.Alunos;

namespace Nap.Application.Features.Alunos;

public record AlunoListaDto(Guid Id, string Nome, string Matricula, Guid CursoId, string Curso, int Semestre,
    Guid ProfissionalId, string Profissional, SituacaoAluno Situacao, int? TotalAtendimentos, int? TotalFaltas);

public record ResumoAcompanhamentoDto(int TotalAtendimentos, int TotalFaltas, DateTimeOffset? UltimoAtendimento);

public record AlunoFichaDto(Guid Id, string Nome, string Matricula, Guid CursoId, string Curso, int Semestre,
    Guid ProfissionalId, string Profissional, SituacaoAluno Situacao, DateOnly InicioAcompanhamento,
    string? NecessidadesEducacionais, string? ObservacoesAcademicas, ResumoAcompanhamentoDto? Resumo,
    string Versao);

public record EventoLinhaDoTempoDto(DateTimeOffset Em, string Tipo, string Titulo, string? Resumo, string Autor);
