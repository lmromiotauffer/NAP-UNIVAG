using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nap.Application.Comum;
using Nap.Application.Features.Notificacoes.Commands;
using Nap.Domain.Comum;
using Nap.Domain.Comunicacoes;
using Nap.Domain.Encaminhamentos;
using Nap.Infrastructure.Persistencia;

namespace Nap.Infrastructure.Notificacoes;

// ponytail: uma instância da API; com várias, travar linhas (UPDLOCK, READPAST)
/// <summary>Entrega os eventos da outbox ao consumidor de notificações, com retentativa e backoff. De hora em hora,
/// também apaga o que ninguém mais lê: refresh tokens expirados e chaves de idempotência antigas.</summary>
public sealed class ProcessadorOutbox(IServiceScopeFactory escopos, IRelogio relogio, IOptions<OpcoesOutbox> opcoes,
    ILogger<ProcessadorOutbox> logger) : BackgroundService
{
    private const int Lote = 50;
    private static readonly TimeSpan IntervaloPurga = TimeSpan.FromHours(1);
    private DateTimeOffset _proximaPurga;

    // Mapa fixo: o Tipo gravado nunca vira um Type arbitrário.
    private static readonly Dictionary<string, Type> Tipos = new()
    {
        [nameof(EncaminhamentoCriado)] = typeof(EncaminhamentoCriado),
        [nameof(RetornoEncaminhamentoRegistrado)] = typeof(RetornoEncaminhamentoRegistrado),
        [nameof(MensagemEnviada)] = typeof(MensagemEnviada),
    };

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(opcoes.Value.IntervaloSegundos));
        // Espera um intervalo antes da primeira varredura: a partida não toca o banco (nos testes da Api, o host sobe antes das migrações).
        while (await timer.WaitForNextTickAsync(ct))
        {
            // Uma falha na varredura (ex.: banco fora do ar) nunca derruba o host; a próxima volta tenta de novo.
            try
            {
                await ProcessarPendentesAsync(ct);
                if (relogio.Agora >= _proximaPurga)
                {
                    await PurgarAsync(ct);
                    _proximaPurga = relogio.Agora + IntervaloPurga;
                }
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                logger.LogError(e, "Falha ao varrer a outbox.");
            }
        }
    }

    /// <returns>Quantidade de mensagens tentadas (com sucesso ou falha).</returns>
    public async Task<int> ProcessarPendentesAsync(CancellationToken ct)
    {
        var agora = relogio.Agora;
        List<Guid> ids;
        await using (var escopo = escopos.CreateAsyncScope())
        {
            ids = await escopo.ServiceProvider.GetRequiredService<NapDbContext>().Outbox
                .Where(m => m.ProcessadoEm == null && m.Tentativas < MensagemOutbox.MaximoTentativas
                    && (m.ProximaTentativaEm == null || m.ProximaTentativaEm <= agora))
                .OrderBy(m => m.OcorridoEm).ThenBy(m => m.Id).Take(Lote)
                .Select(m => m.Id).ToListAsync(ct);
        }
        foreach (var id in ids)
            await ProcessarAsync(id, ct);
        return ids.Count;
    }

    /// <summary>
    /// Refresh tokens expirados há mais de 7 dias (antes disso um sucessor da mesma família ainda pode estar vivo, e
    /// reapresentar o antigo precisa revogá-la) e chaves de idempotência com mais de 1 dia (a repetição vem em segundos).
    /// </summary>
    /// <returns>Linhas apagadas.</returns>
    public async Task<int> PurgarAsync(CancellationToken ct)
    {
        var agora = relogio.Agora;
        await using var escopo = escopos.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<NapDbContext>();
        return await db.TokensRefresh.Where(t => t.ExpiraEm < agora.AddDays(-7)).ExecuteDeleteAsync(ct)
            + await db.ChavesIdempotencia.Where(c => c.CriadaEm < agora.AddDays(-1)).ExecuteDeleteAsync(ct);
    }

    // Um escopo por mensagem: a falha de uma não deixa entidades rastreadas para a próxima.
    private async Task ProcessarAsync(Guid id, CancellationToken ct)
    {
        await using var escopo = escopos.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<NapDbContext>();
        var mensagem = await db.Outbox.SingleAsync(m => m.Id == id, ct);
        try
        {
            var evento = Tipos.TryGetValue(mensagem.Tipo, out var tipo)
                ? (IEventoDominio)JsonSerializer.Deserialize(mensagem.Conteudo, tipo, MensagemOutbox.Json)!
                : throw new InvalidOperationException($"Tipo de evento desconhecido: {mensagem.Tipo}");
            await escopo.ServiceProvider.GetRequiredService<GerarNotificacoesHandler>().Handle(evento, ct);
            mensagem.MarcarProcessada(relogio.Agora);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            // Descarta o que o consumidor deixou pela metade e grava só a falha (tipo e mensagem da exceção, nunca o conteúdo).
            db.ChangeTracker.Clear();
            db.Outbox.Attach(mensagem);
            mensagem.RegistrarFalha($"{e.GetType().Name}: {e.Message}", relogio.Agora);
            if (mensagem.Tentativas >= MensagemOutbox.MaximoTentativas)
                logger.LogError(e, "Mensagem {MensagemId} ({Tipo}) desistida após {Tentativas} tentativas.", id, mensagem.Tipo,
                    mensagem.Tentativas);
            else
                logger.LogWarning(e, "Falha ao processar a mensagem {MensagemId} ({Tipo}); tentativa {Tentativas}.", id, mensagem.Tipo,
                    mensagem.Tentativas);
        }
        await db.SaveChangesAsync(ct);
    }
}
