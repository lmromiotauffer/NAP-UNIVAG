using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Domain.Tests;

public class ValueObjectsTests
{
    [Fact]
    public void Matricula_normaliza_e_rejeita_invalida()
    {
        Assert.Equal("AB-123", Matricula.Criar(" ab-123 ").Valor);
        Assert.Throws<DomainException>(() => Matricula.Criar(""));
        Assert.Throws<DomainException>(() => Matricula.Criar("a b"));
        Assert.Throws<DomainException>(() => Matricula.Criar(new string('A', 21)));
        Assert.Throws<DomainException>(() => Matricula.Criar("ab"));
    }

    private static DateTimeOffset H(int h, int min = 0) => new(2026, 3, 2, h, min, 0, TimeSpan.Zero);

    [Fact]
    public void Intervalo_rejeita_fim_antes_do_inicio_e_mais_de_12h()
    {
        Assert.Throws<DomainException>(() => IntervaloHorario.Criar(H(9), H(8)));
        Assert.Throws<DomainException>(() => IntervaloHorario.Criar(H(9), H(9)));
        Assert.Throws<DomainException>(() => IntervaloHorario.Criar(H(7), H(19, 1)));
        Assert.Equal(H(19), IntervaloHorario.Criar(H(7), H(19)).Fim);
    }

    [Fact]
    public void Vigencia_fim_antes_do_inicio_e_rejeitada()
    {
        var inicio = new DateOnly(2026, 3, 1);
        Assert.Throws<DomainException>(() => PeriodoVigencia.Criar(inicio, inicio.AddDays(-1)));
        Assert.Equal(inicio, PeriodoVigencia.Criar(inicio, inicio).Fim);
        Assert.Equal(PeriodoVigencia.Criar(inicio, inicio.AddDays(1)), PeriodoVigencia.Criar(inicio, inicio.AddDays(1)));
    }

    [Fact]
    public void Intervalo_normaliza_para_utc()
    {
        var i = IntervaloHorario.Criar(
            new DateTimeOffset(2026, 3, 2, 7, 0, 0, TimeSpan.FromHours(-4)),
            new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.FromHours(-4)));
        Assert.Equal(TimeSpan.Zero, i.Inicio.Offset);
        Assert.Equal(11, i.Inicio.Hour);
    }
}
