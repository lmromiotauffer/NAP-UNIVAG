using Nap.Domain.Comum;

namespace Nap.Domain.Agenda;

public sealed record FaixaEscala
{
    private const long TicksQuinzeMinutos = TimeSpan.TicksPerMinute * 15;

    public DayOfWeek Dia { get; }
    public TimeOnly Inicio { get; }
    public TimeOnly Fim { get; }
    public bool SemAtendimento { get; }

    public FaixaEscala(DayOfWeek dia, TimeOnly inicio, TimeOnly fim, bool semAtendimento)
    {
        if (fim <= inicio)
            throw new DomainException("O fim da faixa deve ser depois do início.");
        if (inicio.Ticks % TicksQuinzeMinutos != 0 || fim.Ticks % TicksQuinzeMinutos != 0)
            throw new DomainException("Os horários da faixa devem ser múltiplos de 15 minutos.");
        Dia = dia;
        Inicio = inicio;
        Fim = fim;
        SemAtendimento = semAtendimento;
    }
}
