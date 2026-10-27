using Nap.Domain.Alunos;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Cursos;

namespace Nap.Domain.Tests;

public class AlunoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Autor = Guid.CreateVersion7();

    private static Aluno NovoAluno(Guid? cursoId = null, int semestre = 3) => Aluno.Criar(
        "Maria da Silva", Matricula.Criar("2024001"), cursoId ?? Guid.CreateVersion7(), semestre,
        new DateOnly(2026, 3, 1), Guid.CreateVersion7(), "Necessidade", "Observação", Autor, Agora);

    [Fact]
    public void Aluno_novo_inicia_em_acompanhamento_com_historico()
    {
        var cursoId = Guid.CreateVersion7();
        var a = NovoAluno(cursoId);

        Assert.Equal(SituacaoAluno.EmAcompanhamento, a.Situacao);
        var m = Assert.Single(a.HistoricoSituacao);
        Assert.Null(m.De);
        Assert.Equal(SituacaoAluno.EmAcompanhamento, m.Para);
        var v = Assert.Single(a.HistoricoCursos);
        Assert.Equal(cursoId, v.CursoId);
        Assert.Equal(new DateOnly(2026, 3, 1), v.Desde);
        Assert.Null(v.Ate);
        Assert.Equal(("Cadastro inicial", Autor, Agora), (v.Motivo, v.AutorId, v.RegistradoEm));
    }

    [Fact]
    public void Alterar_situacao_exige_motivo_e_registra_autor()
    {
        var a = NovoAluno();
        Assert.Throws<DomainException>(() => a.AlterarSituacao(SituacaoAluno.Alta, " ", Autor, Agora));

        var outro = Guid.CreateVersion7();
        a.AlterarSituacao(SituacaoAluno.Alta, "Objetivos atingidos", outro, Agora);

        Assert.Equal(SituacaoAluno.Alta, a.Situacao);
        var ultima = a.HistoricoSituacao.Last();
        Assert.Equal(SituacaoAluno.EmAcompanhamento, ultima.De);
        Assert.Equal(outro, ultima.AutorId);
        Assert.Equal(Agora, ultima.Em);
    }

    [Fact]
    public void Alterar_para_mesma_situacao_falha() =>
        Assert.Throws<DomainException>(() =>
            NovoAluno().AlterarSituacao(SituacaoAluno.EmAcompanhamento, "Sem mudança", Autor, Agora));

    [Fact]
    public void Transferencia_fecha_vinculo_anterior_e_abre_novo()
    {
        var a = NovoAluno();
        var novo = Guid.CreateVersion7();
        var data = new DateOnly(2026, 8, 1);
        var outro = Guid.CreateVersion7();
        a.TransferirCurso(novo, data, "  Mudança de curso ", outro, Agora.AddDays(1));

        Assert.Equal(novo, a.CursoAtualId);
        Assert.Equal(2, a.HistoricoCursos.Count);
        Assert.Equal(data, a.HistoricoCursos.First().Ate);
        Assert.Equal(novo, a.HistoricoCursos.Last().CursoId);
        Assert.Equal(data, a.HistoricoCursos.Last().Desde);
        Assert.Null(a.HistoricoCursos.Last().Ate);
        var ultimo = a.HistoricoCursos.Last();
        Assert.Equal(("Mudança de curso", outro, Agora.AddDays(1)), (ultimo.Motivo, ultimo.AutorId, ultimo.RegistradoEm));
    }

    [Fact]
    public void Transferencia_para_mesmo_curso_falha()
    {
        var cursoId = Guid.CreateVersion7();
        var a = NovoAluno(cursoId);
        Assert.Throws<DomainException>(() =>
            a.TransferirCurso(cursoId, new DateOnly(2026, 8, 1), "Motivo", Autor, Agora));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Semestre_fora_de_1_a_20_falha(int semestre) =>
        Assert.Throws<DomainException>(() => NovoAluno(semestre: semestre));

    [Fact]
    public void Curso_valida_nome()
    {
        Assert.Throws<DomainException>(() => Curso.Criar("A"));
        var c = Curso.Criar("Psicologia");
        Assert.True(c.Ativo);
    }

    [Fact]
    public void Atualizar_dados_invalido_nao_altera_nada()
    {
        var a = NovoAluno(semestre: 3);
        Assert.Throws<DomainException>(() => a.AtualizarDados("Novo Nome", 99, "x", "y", Guid.CreateVersion7(), true));
        Assert.Equal("Maria da Silva", a.NomeCompleto);
        Assert.Equal(3, a.Semestre);
        Assert.Equal("Necessidade", a.NecessidadesEducacionais);
    }

    [Fact]
    public void Transferencia_com_data_anterior_ao_vinculo_atual_falha() =>
        Assert.Throws<DomainException>(() =>
            NovoAluno().TransferirCurso(Guid.CreateVersion7(), new DateOnly(2026, 2, 1), "Motivo", Autor, Agora));
}
