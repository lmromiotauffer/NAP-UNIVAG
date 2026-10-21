using Nap.Domain.Comum;

namespace Nap.Domain.Tests;

public class TextoTests
{
    [Fact]
    public void Truncar_respeita_o_limite_sem_partir_emoji()
    {
        Assert.Equal("abc", Texto.Truncar("abc", 3));
        Assert.Equal("ab", Texto.Truncar("abcd", 2));
        Assert.Equal("a…", Texto.Truncar("abcd", 2, "…"));
        // "😀" ocupa duas posições UTF-16: um corte no meio dele recua uma posição.
        Assert.Equal("ab", Texto.Truncar("ab😀cd", 3));
        Assert.Equal("ab😀", Texto.Truncar("ab😀cd", 4));
        Assert.Equal("a…", Texto.Truncar("a😀cd", 3, "…"));
    }
}
