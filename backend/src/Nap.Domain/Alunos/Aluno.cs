using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Domain.Alunos;

public class Aluno : Entidade
{
    private readonly List<MudancaSituacao> _historicoSituacao = [];
    private readonly List<VinculoCurso> _historicoCursos = [];

    public string NomeCompleto { get; private set; } = null!;
    public Matricula Matricula { get; private set; } = null!;
    public bool MatriculaAtiva { get; private set; }
    public Guid CursoAtualId { get; private set; }
    public int Semestre { get; private set; }
    public DateOnly InicioAcompanhamento { get; private set; }
    public Guid ProfissionalResponsavelId { get; private set; }
    public string? NecessidadesEducacionais { get; private set; }
    public string? ObservacoesAcademicas { get; private set; }
    public SituacaoAluno Situacao { get; private set; }

    public IReadOnlyCollection<MudancaSituacao> HistoricoSituacao => _historicoSituacao;
    public IReadOnlyCollection<VinculoCurso> HistoricoCursos => _historicoCursos;

    private Aluno() { } // ORM

    public static Aluno Criar(string nomeCompleto, Matricula matricula, Guid cursoId, int semestre,
        DateOnly inicioAcompanhamento, Guid profissionalResponsavelId, string? necessidadesEducacionais,
        string? observacoesAcademicas, Guid autorId, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(matricula);
        var aluno = new Aluno
        {
            NomeCompleto = Texto.Validar(nomeCompleto, 3, 200, "O nome do aluno"),
            Matricula = matricula,
            MatriculaAtiva = true,
            CursoAtualId = cursoId,
            Semestre = ValidarSemestre(semestre),
            InicioAcompanhamento = inicioAcompanhamento,
            ProfissionalResponsavelId = profissionalResponsavelId,
            NecessidadesEducacionais = necessidadesEducacionais,
            ObservacoesAcademicas = observacoesAcademicas,
            Situacao = SituacaoAluno.EmAcompanhamento,
        };
        aluno._historicoSituacao.Add(MudancaSituacao.Criar(null, SituacaoAluno.EmAcompanhamento, "Cadastro inicial", autorId, agora));
        aluno._historicoCursos.Add(VinculoCurso.Criar(cursoId, inicioAcompanhamento, "Cadastro inicial", autorId, agora));
        return aluno;
    }

    public void AtualizarDados(string nome, int semestre, string? necessidades, string? observacoes,
        Guid profissionalResponsavelId, bool matriculaAtiva)
    {
        var nomeValidado = Texto.Validar(nome, 3, 200, "O nome do aluno");
        var semestreValidado = ValidarSemestre(semestre);
        NomeCompleto = nomeValidado;
        Semestre = semestreValidado;
        NecessidadesEducacionais = necessidades;
        ObservacoesAcademicas = observacoes;
        ProfissionalResponsavelId = profissionalResponsavelId;
        MatriculaAtiva = matriculaAtiva;
    }

    public void AlterarSituacao(SituacaoAluno nova, string motivo, Guid autorId, DateTimeOffset agora)
    {
        motivo = Texto.Validar(motivo, 3, 500, "O motivo");
        if (nova == Situacao)
            throw new DomainException("O aluno já está nesta situação.");
        _historicoSituacao.Add(MudancaSituacao.Criar(Situacao, nova, motivo, autorId, agora));
        Situacao = nova;
    }

    public void TransferirCurso(Guid novoCursoId, DateOnly data, string motivo, Guid autorId, DateTimeOffset agora)
    {
        motivo = Texto.Validar(motivo, 3, 500, "O motivo");
        if (novoCursoId == CursoAtualId)
            throw new DomainException("O aluno já está vinculado a este curso.");
        var atual = _historicoCursos.Last(v => v.Ate is null);
        if (data < atual.Desde)
            throw new DomainException("A data da transferência não pode ser anterior ao início do vínculo atual.");
        atual.Encerrar(data);
        _historicoCursos.Add(VinculoCurso.Criar(novoCursoId, data, motivo, autorId, agora));
        CursoAtualId = novoCursoId;
    }

    private static int ValidarSemestre(int semestre) =>
        semestre is < 1 or > 20 ? throw new DomainException("O semestre deve estar entre 1 e 20.") : semestre;
}
