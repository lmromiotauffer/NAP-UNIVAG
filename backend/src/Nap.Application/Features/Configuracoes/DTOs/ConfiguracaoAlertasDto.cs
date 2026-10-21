namespace Nap.Application.Features.Configuracoes;

public record ConfiguracaoAlertasDto(int FaltasConsecutivas, int FaltasJanela, int JanelaDias, int DiasSemAcolhimento,
    int DiasSemAtualizacao, int DiasPeiPendente, string Versao);
