using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Domain.Alunos;

public class Aluno : Entidade
{
    private readonly List<MudancaSituacao> _historicoSituacao = [];
    private readonly List<VinculoCurso> _historicoCursos = [];

    public string NomeCompleto { get; private set; } = null!;
    public Matricula Matricula { get; private set; } = null!;
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
            NomeCompleto = ValidarNome(nomeCompleto),
            Matricula = matricula,
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
        Guid profissionalResponsavelId)
    {
        var nomeValidado = ValidarNome(nome);
        var semestreValidado = ValidarSemestre(semestre);
        NomeCompleto = nomeValidado;
        Semestre = semestreValidado;
        NecessidadesEducacionais = necessidades;
        ObservacoesAcademicas = observacoes;
        ProfissionalResponsavelId = profissionalResponsavelId;
    }

    public void AlterarSituacao(SituacaoAluno nova, string motivo, Guid autorId, DateTimeOffset agora)
    {
        motivo = ValidarMotivo(motivo);
        if (nova == Situacao)
            throw new DomainException("O aluno já está nesta situação.");
        _historicoSituacao.Add(MudancaSituacao.Criar(Situacao, nova, motivo, autorId, agora));
        Situacao = nova;
    }

    public void TransferirCurso(Guid novoCursoId, DateOnly data, string motivo, Guid autorId, DateTimeOffset agora)
    {
        motivo = ValidarMotivo(motivo);
        if (novoCursoId == CursoAtualId)
            throw new DomainException("O aluno já está vinculado a este curso.");
        var atual = _historicoCursos.Last(v => v.Ate is null);
        if (data < atual.Desde)
            throw new DomainException("A data da transferência não pode ser anterior ao início do vínculo atual.");
        atual.Encerrar(data);
        _historicoCursos.Add(VinculoCurso.Criar(novoCursoId, data, motivo, autorId, agora));
        CursoAtualId = novoCursoId;
    }

    private static string ValidarNome(string nome)
    {
        nome = (nome ?? string.Empty).Trim();
        if (nome.Length is < 3 or > 200)
            throw new DomainException("O nome do aluno deve ter de 3 a 200 caracteres.");
        return nome;
    }

    private static int ValidarSemestre(int semestre) =>
        semestre is < 1 or > 20 ? throw new DomainException("O semestre deve estar entre 1 e 20.") : semestre;

    private static string ValidarMotivo(string motivo)
    {
        motivo = (motivo ?? string.Empty).Trim();
        if (motivo.Length is < 3 or > 500)
            throw new DomainException("O motivo é obrigatório e deve ter de 3 a 500 caracteres.");
        return motivo;
    }
}
