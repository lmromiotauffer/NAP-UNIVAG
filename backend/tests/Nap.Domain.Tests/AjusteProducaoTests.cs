using Nap.Domain.Comum;
using Nap.Domain.Producao;

namespace Nap.Domain.Tests;

public class AjusteProducaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 9, 12, 0, 0, TimeSpan.Zero);

    private static AjusteProducao Novo(CategoriaProducao categoria = CategoriaProducao.ReunioesEquipe, int? ocorrencias = 1,
        int? minutos = 60, string justificativa = "Reunião sem registro") =>
        AjusteProducao.Criar(Guid.CreateVersion7(), new DateOnly(2026, 3, 3), categoria, ocorrencias, minutos, justificativa,
            Guid.CreateVersion7(), Agora);

    [Fact]
    public void Ajuste_valido_guarda_os_dados()
    {
        var a = Novo(ocorrencias: null, minutos: -30);
        Assert.Null(a.Ocorrencias);
        Assert.Equal(-30, a.Minutos);
        Assert.Equal("Reunião sem registro", a.Justificativa);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, 0)]
    [InlineData(null, 0)]
    public void Ajuste_sem_valor_e_rejeitado(int? ocorrencias, int? minutos) =>
        Assert.Throws<DomainException>(() => Novo(ocorrencias: ocorrencias, minutos: minutos));

    [Theory]
    [InlineData(1001, null)]
    [InlineData(-1001, null)]
    [InlineData(null, 10001)]
    [InlineData(null, -10001)]
    public void Ajuste_fora_dos_limites_e_rejeitado(int? ocorrencias, int? minutos) =>
        Assert.Throws<DomainException>(() => Novo(ocorrencias: ocorrencias, minutos: minutos));

    [Fact]
    public void Ajuste_de_ociosidade_e_rejeitado() =>
        Assert.Throws<DomainException>(() => Novo(CategoriaProducao.Ociosidade, null, 30));

    [Theory]
    [InlineData("abcd")]
    [InlineData(" ")]
    public void Ajuste_exige_justificativa(string justificativa) =>
        Assert.Throws<DomainException>(() => Novo(justificativa: justificativa));
}
