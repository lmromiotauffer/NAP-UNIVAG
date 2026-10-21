using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Configuracoes;
using Nap.Domain.Configuracoes;

namespace Nap.Infrastructure.Persistencia;

public sealed class ConfiguracaoAlertasRepositorio(NapDbContext db) : IConfiguracaoAlertas
{
    public async Task<ConfiguracaoAlertasDto> ObterDtoAsync(CancellationToken ct)
    {
        var c = await db.ConfiguracoesAlertas.AsNoTracking()
            .Select(c => new { c.FaltasConsecutivas, c.FaltasJanela, c.JanelaDias, c.DiasSemAcolhimento, c.DiasSemAtualizacao,
                c.DiasPeiPendente, Versao = EF.Property<byte[]>(c, "Versao") })
            .SingleAsync(ct);
        return new ConfiguracaoAlertasDto(c.FaltasConsecutivas, c.FaltasJanela, c.JanelaDias, c.DiasSemAcolhimento,
            c.DiasSemAtualizacao, c.DiasPeiPendente, Convert.ToBase64String(c.Versao));
    }

    public Task<ConfiguracaoAlertas> ObterParaEdicaoAsync(CancellationToken ct) => db.ConfiguracoesAlertas.SingleAsync(ct);
}
