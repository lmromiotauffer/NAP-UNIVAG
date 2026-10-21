using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Domain.Agenda;

public class AutorizacaoForaEscala : Entidade
{
    public Guid ProfissionalId { get; private set; }
    public IntervaloHorario Intervalo { get; private set; } = null!;
    public string Justificativa { get; private set; } = null!;
    public Guid AutorizadoPorId { get; private set; }
    public DateTimeOffset CriadaEm { get; private set; }
    public DateTimeOffset? RevogadaEm { get; private set; }

    private AutorizacaoForaEscala() { } // ORM

    /// <summary>A duração máxima de 12 horas vem do <see cref="IntervaloHorario"/>.</summary>
    public static AutorizacaoForaEscala Criar(Guid profissionalId, IntervaloHorario intervalo, string justificativa,
        Guid autorizadoPorId, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(intervalo);
        justificativa = (justificativa ?? string.Empty).Trim();
        if (justificativa.Length is < 5 or > 500)
            throw new DomainException("A justificativa da autorização é obrigatória e deve ter de 5 a 500 caracteres.");
        return new AutorizacaoForaEscala
        {
            ProfissionalId = profissionalId,
            Intervalo = intervalo,
            Justificativa = justificativa,
            AutorizadoPorId = autorizadoPorId,
            CriadaEm = agora,
        };
    }

    public void Revogar(DateTimeOffset agora)
    {
        if (RevogadaEm is not null)
            throw new DomainException("A autorização já foi revogada.");
        RevogadaEm = agora;
    }
}
