using Nap.Application.Comum;
using Nap.Domain.Comum;

namespace Nap.Application.Features.Producao;

/// <summary>Bloqueia escrita em semana (segunda a domingo, no fuso institucional) já fechada do profissional.</summary>
public sealed class ServicoFechamento(IFechamentoRepository fechamentos, IRelogio relogio)
{
    public const string MensagemSemanaFechada = "A semana está fechada; peça a reabertura à coordenação.";

    public static DateOnly SegundaDe(DateOnly data) => data.AddDays(-(((int)data.DayOfWeek + 6) % 7));

    public async Task ExigirSemanaAbertaAsync(Guid profissionalId, DateOnly data, CancellationToken ct)
    {
        await fechamentos.BloquearAsync(profissionalId, false, ct);
        var fechamento = await fechamentos.ObterAsync(profissionalId, SegundaDe(data), ct);
        if (fechamento is { Fechada: true }) throw new DomainException(MensagemSemanaFechada);
    }

    /// <summary>Usa a data civil de <paramref name="instante"/> no fuso institucional.</summary>
    public Task ExigirSemanaAbertaAsync(Guid profissionalId, DateTimeOffset instante, CancellationToken ct) =>
        ExigirSemanaAbertaAsync(profissionalId, relogio.Fuso.DataLocal(instante), ct);
}
