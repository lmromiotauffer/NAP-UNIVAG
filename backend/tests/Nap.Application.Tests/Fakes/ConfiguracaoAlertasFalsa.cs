using Nap.Application.Comum;
using Nap.Application.Features.Configuracoes;
using Nap.Domain.Configuracoes;

namespace Nap.Application.Tests.Fakes;

/// <summary>Sem <paramref name="opcoes"/>, devolve a <c>Linha</c> (padrão, alterável pelos handlers).</summary>
public sealed class ConfiguracaoAlertasFalsa(ConfiguracaoAlertasDto? opcoes = null) : IConfiguracaoAlertas
{
    public ConfiguracaoAlertas Linha { get; } = ConfiguracaoAlertas.Padrao();

    public Task<ConfiguracaoAlertasDto> ObterDtoAsync(CancellationToken ct) =>
        Task.FromResult(opcoes ?? new ConfiguracaoAlertasDto(Linha.FaltasConsecutivas, Linha.FaltasJanela, Linha.JanelaDias,
            Linha.DiasSemAcolhimento, Linha.DiasSemAtualizacao, Linha.DiasPeiPendente, "v1"));

    public Task<ConfiguracaoAlertas> ObterParaEdicaoAsync(CancellationToken ct) => Task.FromResult(Linha);
}
