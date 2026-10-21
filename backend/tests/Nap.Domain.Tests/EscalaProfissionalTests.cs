using Nap.Domain.Agenda;
using Nap.Domain.Comum;

namespace Nap.Domain.Tests;

public class EscalaProfissionalTests
{
    private static readonly Guid Profissional = Guid.CreateVersion7();
    private static readonly DateOnly D1 = new(2026, 3, 1);

    private static FaixaEscala Faixa(DayOfWeek dia = DayOfWeek.Monday, int ini = 7, int fim = 17) =>
        new(dia, new TimeOnly(ini, 0), new TimeOnly(fim, 0), false);

    private static EscalaProfissional Escala(DateOnly inicio, DateOnly? fim) =>
        EscalaProfissional.Criar(Profissional, inicio, fim, [Faixa()]);

    [Fact]
    public void Faixa_com_fim_antes_do_inicio_e_rejeitada()
    {
        Assert.Throws<DomainException>(() => Faixa(ini: 10, fim: 9));
        Assert.Throws<DomainException>(() => Faixa(ini: 10, fim: 10));
    }

    [Fact]
    public void Faixa_fora_de_multiplo_de_15_minutos_e_rejeitada()
    {
        Assert.Throws<DomainException>(() => new FaixaEscala(DayOfWeek.Monday, new TimeOnly(7, 10), new TimeOnly(17, 0), false));
        Assert.Throws<DomainException>(() => new FaixaEscala(DayOfWeek.Monday, new TimeOnly(7, 0), new TimeOnly(17, 5), false));
        _ = new FaixaEscala(DayOfWeek.Monday, new TimeOnly(7, 15), new TimeOnly(17, 45), false);
    }

    [Fact]
    public void Duas_faixas_no_mesmo_dia_sao_rejeitadas()
    {
        var ex = Assert.Throws<DomainException>(() =>
            EscalaProfissional.Criar(Profissional, D1, null, [Faixa(ini: 7, fim: 12), Faixa(ini: 13, fim: 17)]));
        Assert.Contains("uma faixa por dia", ex.Message);
    }

    [Fact]
    public void Vigencia_aberta_sobrepoe_qualquer_futura()
    {
        var aberta = Escala(D1, null);
        var futura = Escala(new DateOnly(2030, 1, 1), new DateOnly(2030, 6, 1));

        Assert.True(aberta.SobrepoeA(futura));
        Assert.True(futura.SobrepoeA(aberta));
    }

    [Fact]
    public void Vigencias_consecutivas_nao_sobrepoem()
    {
        var a = Escala(D1, new DateOnly(2026, 3, 31));
        var b = Escala(new DateOnly(2026, 4, 1), null);

        Assert.False(a.SobrepoeA(b));
        Assert.False(b.SobrepoeA(a));
        a.AtualizarVigenciaFim(new DateOnly(2026, 4, 1));
        Assert.True(a.SobrepoeA(b));
    }

    [Fact]
    public void Vigente_considera_inicio_e_fim_inclusivos()
    {
        var e = Escala(D1, new DateOnly(2026, 3, 31));

        Assert.True(e.Vigente(D1));
        Assert.True(e.Vigente(new DateOnly(2026, 3, 31)));
        Assert.False(e.Vigente(new DateOnly(2026, 2, 28)));
        Assert.False(e.Vigente(new DateOnly(2026, 4, 1)));
    }

    [Fact]
    public void Vigencia_com_fim_antes_do_inicio_e_rejeitada()
    {
        Assert.Throws<DomainException>(() => Escala(D1, new DateOnly(2026, 2, 1)));
        Assert.Throws<DomainException>(() => Escala(D1, null).AtualizarVigenciaFim(new DateOnly(2026, 2, 1)));
    }

    [Fact]
    public void Inicio_no_fechamento_fica_fora_da_escala()
    {
        var faixa = Escala(D1, null).FaixaDe(DayOfWeek.Monday);

        Assert.Equal(ResultadoEscala.Ok, EscalaProfissional.Avaliar(faixa, new TimeOnly(7, 0)));
        Assert.Equal(ResultadoEscala.Ok, EscalaProfissional.Avaliar(faixa, new TimeOnly(16, 45)));
        Assert.Equal(ResultadoEscala.ForaDaEscala, EscalaProfissional.Avaliar(faixa, new TimeOnly(17, 0)));
        Assert.Equal(ResultadoEscala.ForaDaEscala, EscalaProfissional.Avaliar(faixa, new TimeOnly(6, 45)));
        Assert.Null(Escala(D1, null).FaixaDe(DayOfWeek.Tuesday));
        Assert.Equal(ResultadoEscala.ForaDaEscala, EscalaProfissional.Avaliar(null, new TimeOnly(8, 0)));
    }

    [Fact]
    public void Escala_padrao_expoe_as_mesmas_faixas_como_vo()
    {
        Assert.Equal(EscalaPadrao.Faixas.Count, EscalaPadrao.FaixasEscala.Count);
        _ = EscalaProfissional.Criar(Profissional, D1, null, EscalaPadrao.FaixasEscala);
    }
}
