using Nap.Application.Comum;
using Nap.Domain.Agenda;
using Nap.Domain.Comum;

namespace Nap.Application.Features.Agenda;

/// <summary>Escala vigente do profissional (ou a padrão) e feriados, no fuso institucional.</summary>
public sealed class ServicoEscala(IEscalaRepository escalas, IFeriadoRepository feriados, IRelogio relogio)
{
    public async Task<FaixaEscala?> FaixaVigenteAsync(Guid profissionalId, DateOnly data, CancellationToken ct) =>
        FaixaDe(await escalas.ObterVigenteAsync(profissionalId, data, ct), data);

    /// <summary>Sem escala vigente, vale a padrão. Decisão: na agenda isso inclui o Administrador (pode marcar reuniões das 07h
    /// às 17h), mas a produção não lhe dá capacidade sem escala própria (ObterProducao); admin que atende cadastra escala.</summary>
    public static FaixaEscala? FaixaDe(EscalaProfissional? escala, DateOnly data) =>
        escala is null ? EscalaPadrao.FaixasEscala.SingleOrDefault(f => f.Dia == data.DayOfWeek) : escala.FaixaDe(data.DayOfWeek);

    /// <summary>
    /// O início precisa cair numa faixa da escala do dia (≥ abertura e &lt; fechamento; o fim pode passar do fechamento) e a
    /// data não pode ser feriado ativo. "Sem atendimento" não bloqueia: só sinaliza na grade. Uma autorização ativa do profissional
    /// que cobre o início vale em qualquer dia, inclusive feriado (o tempo fora da faixa vira extra na produção).
    /// </summary>
    public async Task ExigirDentroDaEscalaAsync(Guid profissionalId, DateTimeOffset inicio, CancellationToken ct)
    {
        if (await escalas.ExisteAutorizacaoAtivaAsync(profissionalId, inicio, ct)) return;
        var local = TimeZoneInfo.ConvertTime(inicio, relogio.Fuso).DateTime;
        var data = DateOnly.FromDateTime(local);
        if (await feriados.ObterAtivoAsync(data, ct) is { } feriado)
            throw new DomainException($"Esta data é feriado: {feriado.Descricao}.");
        var faixa = await FaixaVigenteAsync(profissionalId, data, ct);
        if (EscalaProfissional.Avaliar(faixa, TimeOnly.FromDateTime(local)) != ResultadoEscala.Ok)
            throw new DomainException("Fora da escala do profissional neste dia.");
    }
}
