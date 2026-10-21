using System.Text.Json;
using System.Text.Json.Serialization;
using Nap.Domain.Comum;

namespace Nap.Infrastructure.Persistencia;

/// <summary>Evento de domínio gravado na mesma transação do agregado; processado depois, fora da requisição.</summary>
public class MensagemOutbox
{
    public const int MaximoTentativas = 8;

    /// <summary>Único options para serializar e desserializar os eventos (enums como texto).</summary>
    public static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public Guid Id { get; private set; }
    public string Tipo { get; private set; } = null!;
    public string Conteudo { get; private set; } = null!;
    public DateTimeOffset OcorridoEm { get; private set; }
    public DateTimeOffset? ProcessadoEm { get; private set; }
    public int Tentativas { get; private set; }
    public DateTimeOffset? ProximaTentativaEm { get; private set; }
    public string? UltimoErro { get; private set; }

    private MensagemOutbox() { } // ORM

    public MensagemOutbox(IEventoDominio evento)
    {
        Id = evento.EventoId;
        Tipo = evento.GetType().Name;
        Conteudo = JsonSerializer.Serialize(evento, evento.GetType(), Json);
        OcorridoEm = evento.OcorridoEm;
    }

    public void MarcarProcessada(DateTimeOffset agora) => ProcessadoEm = agora;

    /// <summary>Backoff exponencial: 30 s × 2^(tentativas−1), teto de 1 h.</summary>
    public void RegistrarFalha(string erro, DateTimeOffset agora)
    {
        Tentativas++;
        UltimoErro = erro.Length > 2000 ? erro[..2000] : erro;
        var espera = TimeSpan.FromSeconds(30 * Math.Pow(2, Tentativas - 1));
        ProximaTentativaEm = agora + (espera < TimeSpan.FromHours(1) ? espera : TimeSpan.FromHours(1));
    }
}
