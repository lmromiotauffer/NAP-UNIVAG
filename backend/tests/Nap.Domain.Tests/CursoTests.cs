using Nap.Domain.Comum;
using Nap.Domain.Cursos;

namespace Nap.Domain.Tests;

public class CursoTests
{
    [Fact]
    public void Curso_inativo_reativa_e_renomeia()
    {
        var curso = Curso.Criar("Direito");

        curso.Inativar();
        Assert.False(curso.Ativo);
        curso.Reativar();
        Assert.True(curso.Ativo);
        curso.Renomear("  Ciências Jurídicas ");
        Assert.Equal("Ciências Jurídicas", curso.Nome);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public void Renomear_com_nome_invalido_e_rejeitado_sem_alterar(string nome)
    {
        var curso = Curso.Criar("Direito");

        Assert.Throws<DomainException>(() => curso.Renomear(nome));
        Assert.Equal("Direito", curso.Nome);
    }
}
