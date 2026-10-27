using Nap.Domain.Agenda;
using Nap.Domain.Atividades;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Domain.Tests;

public class AtividadeCursoTests
{
    private static readonly DateTimeOffset Agora = CompromissoTests.Agora;
    private static readonly Guid Autor = CompromissoTests.Autor;

    private static AtividadeCurso Avulsa() => AtividadeCurso.RegistrarAvulsa(
        Autor, CompromissoTests.Intervalo(7), Guid.CreateVersion7(), TipoAtividade.RodaConversa,
        "Roda de conversa", "Atividade não agendada", Autor, Agora);

    [Fact]
    public void Registro_de_compromisso_conclui_o_compromisso()
    {
        var c = CompromissoTests.Novo(TipoCompromisso.AtividadeCurso);
        var cursoId = Guid.CreateVersion7();
        var a = AtividadeCurso.RegistrarDeCompromisso(c, CompromissoTests.Intervalo(8), cursoId,
            TipoAtividade.FormacaoDocente, "  Formação  ", Autor, Agora);

        Assert.Equal(EstadoCompromisso.Concluido, c.Estado);
        Assert.Equal(a.Id, c.RegistroVinculadoId);
        Assert.Equal(c.Id, a.CompromissoId);
        Assert.Equal(c.ProfissionalId, a.ProfissionalId);
        Assert.Equal(cursoId, a.CursoId);
        Assert.Equal("Formação", a.Descricao);
        Assert.Null(a.JustificativaAvulso);
    }

    [Theory]
    [InlineData(TipoCompromisso.Atendimento)]
    [InlineData(TipoCompromisso.Bloqueio)]
    public void Compromisso_que_nao_e_atividade_no_curso_e_rejeitado(TipoCompromisso tipo)
    {
        var c = CompromissoTests.Novo(tipo, tipo == TipoCompromisso.Atendimento ? Guid.CreateVersion7() : null);
        var ex = Assert.Throws<DomainException>(() => AtividadeCurso.RegistrarDeCompromisso(
            c, CompromissoTests.Intervalo(7), null, TipoAtividade.Outra, "Descrição", Autor, Agora));
        Assert.Equal("Só é possível registrar atividade a partir de um compromisso de atividade no curso agendado.", ex.Message);
        Assert.Equal(EstadoCompromisso.Agendado, c.Estado);
    }

    [Fact]
    public void Compromisso_cancelado_e_rejeitado()
    {
        var c = CompromissoTests.Novo(TipoCompromisso.AtividadeCurso);
        c.Cancelar("Motivo");
        Assert.Throws<DomainException>(() => AtividadeCurso.RegistrarDeCompromisso(
            c, CompromissoTests.Intervalo(7), null, TipoAtividade.Outra, "Descrição", Autor, Agora));
    }

    [Fact]
    public void Avulsa_exige_justificativa()
    {
        foreach (var j in new[] { null, " ", "abc", new string('x', 501) })
            Assert.Throws<DomainException>(() => AtividadeCurso.RegistrarAvulsa(
                Autor, CompromissoTests.Intervalo(7), null, TipoAtividade.Outra, "Descrição", j!, Autor, Agora));
        Assert.Equal("Atividade não agendada", Avulsa().JustificativaAvulso);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void Descricao_fora_do_tamanho_e_rejeitada(string descricao) =>
        Assert.Throws<DomainException>(() => AtividadeCurso.RegistrarAvulsa(
            Autor, CompromissoTests.Intervalo(7), null, TipoAtividade.Outra, descricao, "Justificativa", Autor, Agora));

    [Fact]
    public void Descricao_acima_de_2000_e_rejeitada() =>
        Assert.Throws<DomainException>(() => AtividadeCurso.RegistrarAvulsa(
            Autor, CompromissoTests.Intervalo(7), null, TipoAtividade.Outra, new string('x', 2001), "Justificativa", Autor, Agora));

    [Fact]
    public void Atividade_que_ainda_nao_terminou_e_rejeitada()
    {
        var c = CompromissoTests.Novo(TipoCompromisso.AtividadeCurso);
        var ex = Assert.Throws<DomainException>(() => AtividadeCurso.RegistrarDeCompromisso(
            c, CompromissoTests.Intervalo(12), null, TipoAtividade.Outra, "Descrição", Autor, Agora));
        Assert.Equal("Não é possível registrar atividade que ainda não terminou.", ex.Message);
        Assert.Equal(EstadoCompromisso.Agendado, c.Estado);
    }

    [Fact]
    public void Correcao_exige_justificativa_e_guarda_o_anterior()
    {
        var a = Avulsa();
        var cursoAnterior = a.CursoId;
        var inicioAnterior = a.Intervalo.Inicio;
        foreach (var j in new[] { null, " ", "abc", new string('x', 501) })
            Assert.Throws<DomainException>(() => a.Corrigir(
                CompromissoTests.Intervalo(8), null, TipoAtividade.Outra, "Nova", j!, Autor, Agora));
        Assert.Empty(a.Correcoes);

        a.Corrigir(CompromissoTests.Intervalo(8), null, TipoAtividade.Outra, " Nova descrição ", " Erro de digitação ", Autor, Agora);

        var cor = Assert.Single(a.Correcoes);
        Assert.Equal("Erro de digitação", cor.Justificativa);
        Assert.Equal(inicioAnterior, cor.InicioAnterior);
        Assert.Equal(cursoAnterior, cor.CursoIdAnterior);
        Assert.Equal(TipoAtividade.RodaConversa, cor.TipoAnterior);
        Assert.Equal("Roda de conversa", cor.DescricaoAnterior);
        Assert.Equal(Autor, cor.AutorId);
        Assert.Null(a.CursoId);
        Assert.Equal(TipoAtividade.Outra, a.Tipo);
        Assert.Equal("Nova descrição", a.Descricao);
        Assert.Equal(CompromissoTests.Intervalo(8), a.Intervalo);
    }

    [Fact]
    public void Correcao_nao_aceita_atividade_que_ainda_nao_terminou() =>
        Assert.Throws<DomainException>(() => Avulsa().Corrigir(
            CompromissoTests.Intervalo(12), null, TipoAtividade.Outra, "Nova", "Justificativa", Autor, Agora));

    [Theory]
    [InlineData(0, 0, Turno.Manha)]
    [InlineData(11, 59, Turno.Manha)]
    [InlineData(12, 0, Turno.Tarde)]
    [InlineData(17, 59, Turno.Tarde)]
    [InlineData(18, 0, Turno.Noite)]
    public void Turno_pelo_horario_de_inicio(int h, int m, Turno esperado) =>
        Assert.Equal(esperado, AtividadeCurso.TurnoDe(new TimeOnly(h, m)));
}
