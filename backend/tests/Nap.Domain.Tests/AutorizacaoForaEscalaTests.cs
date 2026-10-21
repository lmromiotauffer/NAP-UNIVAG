using Nap.Domain.Agenda;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Domain.Tests;

public class AutorizacaoForaEscalaTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 10, 5, 20, 0, 0, TimeSpan.Zero);
    private static readonly Guid Profissional = Guid.NewGuid();
    private static readonly Guid Autor = Guid.NewGuid();

    private static AutorizacaoForaEscala Nova(string justificativa = "Plantão extra", int horas = 2) =>
        AutorizacaoForaEscala.Criar(Profissional, IntervaloHorario.Criar(Inicio, Inicio.AddHours(horas)), justificativa, Autor, Inicio.AddDays(-1));

    [Theory]
    [InlineData("")]
    [InlineData("  abcd  ")]
    public void Autorizacao_exige_justificativa_de_5_a_500(string justificativa)
    {
        Assert.Throws<DomainException>(() => Nova(justificativa));
        Assert.Throws<DomainException>(() => Nova(new string('x', 501)));
        Assert.Equal("abcde", Nova("  abcde ").Justificativa);
        Assert.Equal(500, Nova(new string('x', 500)).Justificativa.Length);
    }

    [Fact]
    public void Autorizacao_de_ate_12_horas()
    {
        Assert.Equal(Inicio.AddHours(12), Nova(horas: 12).Intervalo.Fim);
        Assert.Throws<DomainException>(() => Nova(horas: 13));
    }

    [Fact]
    public void Revogada_nao_cobre()
    {
        var a = Nova();
        a.Revogar(Inicio);
        Assert.Equal(Inicio, a.RevogadaEm);
    }

    [Fact]
    public void Revogar_duas_vezes_falha()
    {
        var a = Nova();
        a.Revogar(Inicio);
        Assert.Throws<DomainException>(() => a.Revogar(Inicio));
    }

    [Theory]
    [InlineData(TipoCompromisso.ReuniaoEquipe, true)]
    [InlineData(TipoCompromisso.ReuniaoCoordenacaoGestao, true)]
    [InlineData(TipoCompromisso.Lembrete, true)]
    [InlineData(TipoCompromisso.Atendimento, false)]
    [InlineData(TipoCompromisso.ElaboracaoPei, false)]
    [InlineData(TipoCompromisso.TarefaAdministrativa, false)]
    [InlineData(TipoCompromisso.EncaminhamentoDevolutiva, false)]
    [InlineData(TipoCompromisso.AtividadeCurso, false)]
    [InlineData(TipoCompromisso.Bloqueio, false)]
    public void Tipos_que_permitem_grupo(TipoCompromisso tipo, bool esperado) =>
        Assert.Equal(esperado, tipo.PermiteGrupo());
}
