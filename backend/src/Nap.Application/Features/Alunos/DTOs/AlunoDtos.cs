using Nap.Domain.Alunos;

namespace Nap.Application.Features.Alunos;

/// <summary>Para o coordenador de curso de um aluno já transferido para outro curso, o estado atual (curso, profissional,
/// situação e textos) chega <c>null</c>/vazio: ele só vê o período em que coordenava.</summary>
public record AlunoListaDto(Guid Id, string Nome, string Matricula, bool? MatriculaAtiva, Guid? CursoId, string Curso, int? Semestre,
    Guid? ProfissionalId, string Profissional, SituacaoAluno? Situacao, int? TotalAtendimentos, int? TotalFaltas);

public record ResumoAcompanhamentoDto(int TotalAtendimentos, int TotalFaltas, DateTimeOffset? UltimoAtendimento);

/// <summary>Mesmo recorte de <see cref="AlunoListaDto"/> para o coordenador de um aluno já transferido.</summary>
public record AlunoFichaDto(Guid Id, string Nome, string Matricula, bool? MatriculaAtiva, Guid? CursoId, string Curso, int? Semestre,
    Guid? ProfissionalId, string Profissional, SituacaoAluno? Situacao, DateOnly InicioAcompanhamento,
    string? NecessidadesEducacionais, string? ObservacoesAcademicas, ResumoAcompanhamentoDto? Resumo,
    string Versao);

public record EventoLinhaDoTempoDto(DateTimeOffset Em, string Tipo, string Titulo, string? Resumo, string Autor);
