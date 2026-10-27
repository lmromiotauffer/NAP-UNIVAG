using Nap.Infrastructure.Comum;

namespace Nap.Infrastructure.Tests;

public class FormatoProducaoTests
{
    [Theory]
    [InlineData(-30, "-0:30")]
    [InlineData(-125, "-2:05")]
    [InlineData(0, "0:00")]
    [InlineData(90, "+1:30")]
    public void Variacao_de_duracao_leva_sinal_e_minutos_com_dois_digitos(int minutos, string esperado) =>
        Assert.Equal(esperado, FormatoProducao.VariacaoDuracao(minutos));
}
