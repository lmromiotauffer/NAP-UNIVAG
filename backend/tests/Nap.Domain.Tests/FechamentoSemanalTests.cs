using Nap.Domain.Comum;
using Nap.Domain.Producao;

namespace Nap.Domain.Tests;

public class FechamentoSemanalTests
{
    private static readonly DateOnly Segunda = new(2026, 3, 2);
    private static readonly DateTimeOffset Agora = new(2026, 3, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Autor = Guid.CreateVersion7();

    [Fact]
    public void Semana_deve_comecar_na_segunda() =>
        Assert.Throws<DomainException>(() => FechamentoSemanal.Criar(Guid.CreateVersion7(), Segunda.AddDays(1)));

    [Fact]
    public void Fechar_semana_nao_encerrada_e_rejeitado()
    {
        var f = FechamentoSemanal.Criar(Guid.CreateVersion7(), Segunda);
        var ex = Assert.Throws<DomainException>(() => f.Fechar(Autor, Agora, Segunda.AddDays(6)));
        Assert.Equal("Só é possível fechar uma semana já encerrada.", ex.Message);
        Assert.False(f.Fechada);

        f.Fechar(Autor, Agora, Segunda.AddDays(7));
        Assert.True(f.Fechada);
        Assert.Throws<DomainException>(() => f.Fechar(Autor, Agora, Segunda.AddDays(7)));
    }

    [Fact]
    public void Reabrir_exige_justificativa_e_guarda_historico()
    {
        var f = FechamentoSemanal.Criar(Guid.CreateVersion7(), Segunda);
        Assert.Throws<DomainException>(() => f.Reabrir("Corrigir atendimento", Autor, Agora));

        f.Fechar(Autor, Agora, Segunda.AddDays(7));
        Assert.Throws<DomainException>(() => f.Reabrir("abc", Autor, Agora));
        Assert.Throws<DomainException>(() => f.Reabrir(new string('x', 501), Autor, Agora));

        var coordenadora = Guid.CreateVersion7();
        f.Reabrir("  Corrigir atendimento  ", coordenadora, Agora.AddHours(1));
        Assert.False(f.Fechada);
        Assert.Collection(f.Eventos,
            e => { Assert.Equal(TipoEventoFechamento.Fechamento, e.Tipo); Assert.Equal(Autor, e.AutorId); Assert.Null(e.Justificativa); },
            e =>
            {
                Assert.Equal(TipoEventoFechamento.Reabertura, e.Tipo);
                Assert.Equal(coordenadora, e.AutorId);
                Assert.Equal(Agora.AddHours(1), e.Em);
                Assert.Equal("Corrigir atendimento", e.Justificativa);
            });
    }
}
