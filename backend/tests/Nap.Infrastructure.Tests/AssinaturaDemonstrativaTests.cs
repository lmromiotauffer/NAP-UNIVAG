using Nap.Domain.Peis;
using Nap.Infrastructure.Assinatura;

namespace Nap.Infrastructure.Tests;

public class AssinaturaDemonstrativaTests
{
    private static readonly DateTimeOffset Instante = new(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(-4));

    [Fact]
    public async Task Assinatura_demonstrativa_e_deterministica_e_identifica_o_metodo()
    {
        var ct = TestContext.Current.CancellationToken;
        var servico = new AssinaturaDemonstrativa();
        var versao = Guid.NewGuid();
        var hash = new string('a', 64);

        var a = await servico.AssinarAsync(versao, PapelAssinatura.Aluno, "Fulano", hash, Instante, ct);
        var b = await servico.AssinarAsync(versao, PapelAssinatura.Aluno, "Fulano", hash, Instante, ct);
        var outroPapel = await servico.AssinarAsync(versao, PapelAssinatura.Profissional, "Fulano", hash, Instante, ct);

        Assert.Equal("Demonstrativa", a.Metodo);
        Assert.Equal(a, b);
        Assert.Matches("^[0-9a-f]{64}$", a.Evidencia);
        Assert.NotEqual(a.Evidencia, outroPapel.Evidencia);
    }

    [Fact]
    public async Task Separador_no_nome_nao_colide_com_outro_campo()
    {
        var ct = TestContext.Current.CancellationToken;
        var servico = new AssinaturaDemonstrativa();
        var versao = Guid.NewGuid();

        var a = await servico.AssinarAsync(versao, PapelAssinatura.Aluno, "Fulano|b", "c", Instante, ct);
        var b = await servico.AssinarAsync(versao, PapelAssinatura.Aluno, "Fulano", "b|c", Instante, ct);

        Assert.NotEqual(a.Evidencia, b.Evidencia);
    }
}
