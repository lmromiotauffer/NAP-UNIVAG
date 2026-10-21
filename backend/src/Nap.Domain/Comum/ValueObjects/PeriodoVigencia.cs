namespace Nap.Domain.Comum.ValueObjects;

public sealed record PeriodoVigencia
{
    public DateOnly Inicio { get; }
    public DateOnly Fim { get; }

    private PeriodoVigencia() { } // ORM

    private PeriodoVigencia(DateOnly inicio, DateOnly fim)
    {
        Inicio = inicio;
        Fim = fim;
    }

    public static PeriodoVigencia Criar(DateOnly inicio, DateOnly fim)
    {
        if (fim < inicio)
            throw new DomainException("O fim da vigência não pode ser anterior ao início.");
        return new PeriodoVigencia(inicio, fim);
    }
}
