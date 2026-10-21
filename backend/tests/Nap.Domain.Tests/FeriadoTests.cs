using Nap.Domain.Agenda;
using Nap.Domain.Comum;

namespace Nap.Domain.Tests;

public class FeriadoTests
{
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public void Feriado_descricao_obrigatoria(string descricao)
    {
        Assert.Throws<DomainException>(() => Feriado.Criar(new DateOnly(2026, 9, 7), descricao));
    }

    [Fact]
    public void Criar_normaliza_descricao_e_inativar_desativa()
    {
        var f = Feriado.Criar(new DateOnly(2026, 9, 7), "  Independência  ");

        Assert.Equal("Independência", f.Descricao);
        Assert.True(f.Ativo);
        f.Inativar();
        Assert.False(f.Ativo);
    }
}
