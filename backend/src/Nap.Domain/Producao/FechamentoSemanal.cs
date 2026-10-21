using Nap.Domain.Comum;

namespace Nap.Domain.Producao;

public class FechamentoSemanal : Entidade
{
    private readonly List<EventoFechamento> _eventos = [];

    public Guid ProfissionalId { get; private set; }
    /// <summary>Segunda-feira que identifica a semana (segunda a domingo).</summary>
    public DateOnly Semana { get; private set; }
    public bool Fechada { get; private set; }

    public IReadOnlyCollection<EventoFechamento> Eventos => _eventos;

    private FechamentoSemanal() { } // ORM

    public static FechamentoSemanal Criar(Guid profissionalId, DateOnly semana)
    {
        if (semana.DayOfWeek != DayOfWeek.Monday)
            throw new DomainException("A semana deve ser identificada pela segunda-feira.");
        return new FechamentoSemanal { ProfissionalId = profissionalId, Semana = semana };
    }

    /// <param name="hoje">Data civil de hoje no fuso institucional (o domínio não lê relógio).</param>
    public void Fechar(Guid autorId, DateTimeOffset agora, DateOnly hoje)
    {
        if (Fechada)
            throw new DomainException("A semana já está fechada.");
        if (Semana.AddDays(6) >= hoje)
            throw new DomainException("Só é possível fechar uma semana já encerrada.");
        Fechada = true;
        _eventos.Add(EventoFechamento.Criar(TipoEventoFechamento.Fechamento, autorId, agora, null));
    }

    public void Reabrir(string justificativa, Guid autorId, DateTimeOffset agora)
    {
        if (!Fechada)
            throw new DomainException("A semana já está aberta.");
        justificativa = (justificativa ?? string.Empty).Trim();
        if (justificativa.Length is < 5 or > 500)
            throw new DomainException("A justificativa da reabertura é obrigatória e deve ter de 5 a 500 caracteres.");
        Fechada = false;
        _eventos.Add(EventoFechamento.Criar(TipoEventoFechamento.Reabertura, autorId, agora, justificativa));
    }
}
