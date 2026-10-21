using Nap.Application.Comum;
using Nap.Application.Features.Configuracoes;
using Nap.Application.Features.Configuracoes.Commands;
using Nap.Application.Features.Painel.Queries;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Comum;

namespace Nap.Application.Tests;

public class ConfiguracaoAlertasTests
{
    private readonly ConfiguracaoAlertasFalsa _config = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new();
    private readonly CacheConsultasFalso _cache = new();

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private AtualizarConfiguracaoAlertasHandler Handler(string perfil = Perfis.Administrador) =>
        new(new AtualizarConfiguracaoAlertasValidator(), _config, new UsuarioAtualFalso(perfil), _auditoria, _uow, _cache);

    private static AtualizarConfiguracaoAlertasCommand Comando(int faltasConsecutivas = 4, int diasSemAtualizacao = 20, string versao = "v1") =>
        new(faltasConsecutivas, 5, 60, 10, diasSemAtualizacao, 12, versao);

    [Fact]
    public async Task Atualizar_exige_administracao()
    {
        foreach (var perfil in new[] { Perfis.ProfissionalNap, Perfis.Sip, Perfis.Gestao, Perfis.CoordenadorCurso })
            await Assert.ThrowsAsync<ForbiddenException>(() => Handler(perfil).Handle(Comando(), Ct));
        Assert.Equal(0, _uow.Salvamentos);

        await Handler(Perfis.CoordenadorNap).Handle(Comando(), Ct);
        var dto = await Handler().Handle(Comando(diasSemAtualizacao: 30), Ct);
        Assert.Equal((4, 5, 60, 10, 30, 12), (dto.FaltasConsecutivas, dto.FaltasJanela, dto.JanelaDias, dto.DiasSemAcolhimento,
            dto.DiasSemAtualizacao, dto.DiasPeiPendente));
        Assert.Equal("configuracao.alertas", _auditoria.Registros[0].Acao);
    }

    [Fact]
    public async Task Fora_das_faixas_retorna_400_no_campo()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Handler().Handle(new AtualizarConfiguracaoAlertasCommand(0, 51, 6, 91, 181, 181, "v1"), Ct));
        Assert.Equal(["diasPeiPendente", "diasSemAcolhimento", "diasSemAtualizacao", "faltasConsecutivas", "faltasJanela", "janelaDias"],
            ex.Erros.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(0, _uow.Salvamentos);
        Assert.Equal(3, _config.Linha.FaltasConsecutivas);
    }

    [Fact]
    public async Task Atualizar_invalida_o_cache_do_painel()
    {
        await Handler().Handle(Comando(), Ct);
        Assert.Equal(1, _cache.Invalidacoes);
    }

    [Fact]
    public async Task Versao_antiga_retorna_conflito()
    {
        _uow.VersaoAtual = "v2";
        await Assert.ThrowsAsync<ConflictException>(() => Handler().Handle(Comando(versao: "v1"), Ct));
        Assert.Equal((0, 0), (_uow.Salvamentos, _cache.Invalidacoes));
        Assert.Equal(3, _config.Linha.FaltasConsecutivas);
    }

    [Fact]
    public async Task Painel_informa_o_limite_de_dias_sem_atualizacao()
    {
        var consultas = new PainelQueriesFalso { Encaminhamentos = new(5, 2, 1, 3, 1) };
        var handler = new ObterPainelHandler(new ObterPainelValidator(),
            consultas, new CacheConsultasFalso(), new ConfiguracaoAlertasFalsa(new ConfiguracaoAlertasDto(3, 3, 30, 7, 21, 15, "v1")),
            new UsuarioAtualFalso(Perfis.Sip), new RelogioFixo());

        var dto = await handler.Handle(new ObterPainelQuery(), Ct);
        Assert.Equal(21, dto.Encaminhamentos!.DiasSemAtualizacao);
    }
}
