using Nap.Application.Comum;

namespace Nap.Application.Tests;

public class FusoExtensoesTests
{
    [Fact]
    public void Inicio_do_dia_sem_meia_noite_no_horario_de_verao_vale_o_primeiro_horario_valido()
    {
        // Em 04/11/2018 Cuiabá saltou de 00:00 para 01:00 (-03:00): a meia-noite local não existe.
        var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Cuiaba");

        Assert.Equal(new DateTimeOffset(2018, 11, 4, 4, 0, 0, TimeSpan.Zero), fuso.InicioDoDia(new DateOnly(2018, 11, 4)));
    }
}
