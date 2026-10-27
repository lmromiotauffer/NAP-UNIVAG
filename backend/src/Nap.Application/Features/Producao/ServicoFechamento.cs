using Nap.Application.Comum;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

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

    public const string MensagemPeriodoFechado = "O período alcança uma semana já fechada; reabra a semana antes.";

    /// <summary>
    /// Escala e feriado mudam a capacidade de todo o período, e a produção é recalculada a partir deles: nenhum dia de
    /// <paramref name="de"/> a <paramref name="ate"/> (nulo = sem fim) pode cair em semana fechada. Profissional nulo = todos.
    /// </summary>
    public async Task ExigirPeriodoAbertoAsync(Guid? profissionalId, DateOnly de, DateOnly? ate, CancellationToken ct)
    {
        // shortcut: sem profissional (feriado) não há trava; um fechamento simultâneo ao cadastro escapa da checagem.
        if (profissionalId is { } id) await fechamentos.BloquearAsync(id, false, ct);
        if (await fechamentos.ExisteFechadaAsync(profissionalId, SegundaDe(de), ate, ct))
            throw new DomainException(MensagemPeriodoFechado);
    }

    /// <summary>
    /// A produção desconta o intervalo de todos os dias que ele toca: um que vira a semana (ex.: bloqueio de domingo à noite
    /// até segunda) exige as duas semanas abertas. Datas civis no fuso institucional; o fim é exclusivo.
    /// </summary>
    public async Task ExigirSemanaAbertaAsync(Guid profissionalId, IntervaloHorario intervalo, CancellationToken ct)
    {
        var inicio = relogio.Fuso.DataLocal(intervalo.Inicio);
        await ExigirSemanaAbertaAsync(profissionalId, inicio, ct);
        var segundaDoFim = SegundaDe(relogio.Fuso.DataLocal(intervalo.Fim.AddTicks(-1)));
        if (segundaDoFim != SegundaDe(inicio) && await fechamentos.ObterAsync(profissionalId, segundaDoFim, ct) is { Fechada: true })
            throw new DomainException(MensagemSemanaFechada);
    }
}
