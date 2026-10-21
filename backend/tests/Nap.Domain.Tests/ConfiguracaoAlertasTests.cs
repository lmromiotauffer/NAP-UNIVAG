using Nap.Domain.Comum;
using Nap.Domain.Configuracoes;

namespace Nap.Domain.Tests;

public class ConfiguracaoAlertasTests
{
    [Fact]
    public void Padrao_traz_os_valores_iniciais()
    {
        var c = ConfiguracaoAlertas.Padrao();

        Assert.Equal((3, 3, 30, 7, 15, 15),
            (c.FaltasConsecutivas, c.FaltasJanela, c.JanelaDias, c.DiasSemAcolhimento, c.DiasSemAtualizacao, c.DiasPeiPendente));
    }

    [Fact]
    public void Atualizar_aceita_os_limites()
    {
        var c = ConfiguracaoAlertas.Padrao();
        c.Atualizar(20, 50, 365, 90, 180, 180);
        Assert.Equal((20, 50, 365, 90, 180, 180),
            (c.FaltasConsecutivas, c.FaltasJanela, c.JanelaDias, c.DiasSemAcolhimento, c.DiasSemAtualizacao, c.DiasPeiPendente));
        c.Atualizar(1, 1, 7, 1, 1, 1);
        Assert.Equal((1, 1, 7, 1, 1, 1),
            (c.FaltasConsecutivas, c.FaltasJanela, c.JanelaDias, c.DiasSemAcolhimento, c.DiasSemAtualizacao, c.DiasPeiPendente));
    }

    [Theory]
    [InlineData(0, 3, 30, 7, 15, 15)]
    [InlineData(21, 3, 30, 7, 15, 15)]
    [InlineData(3, 0, 30, 7, 15, 15)]
    [InlineData(3, 51, 30, 7, 15, 15)]
    [InlineData(3, 3, 6, 7, 15, 15)]
    [InlineData(3, 3, 366, 7, 15, 15)]
    [InlineData(3, 3, 30, 0, 15, 15)]
    [InlineData(3, 3, 30, 91, 15, 15)]
    [InlineData(3, 3, 30, 7, 0, 15)]
    [InlineData(3, 3, 30, 7, 181, 15)]
    [InlineData(3, 3, 30, 7, 15, 0)]
    [InlineData(3, 3, 30, 7, 15, 181)]
    public void Configuracao_fora_das_faixas_e_rejeitada(int a, int b, int c, int d, int e, int f)
    {
        var cfg = ConfiguracaoAlertas.Padrao();

        Assert.Throws<DomainException>(() => cfg.Atualizar(a, b, c, d, e, f));
        Assert.Equal(3, cfg.FaltasConsecutivas);
        Assert.Equal(30, cfg.JanelaDias);
    }
}
