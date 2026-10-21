using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Configuracoes.Commands;
using Nap.Application.Features.Encaminhamentos;
using Nap.Domain.Comum;
using Nap.Domain.Encaminhamentos;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Tests;

public class ConfiguracaoAlertasTests(SqlServerFixture fx)
{
    [Fact]
    public async Task Configuracao_inicial_tem_os_padroes()
    {
        await using var db = fx.CriarContexto();
        var o = await new ConfiguracaoAlertasRepositorio(db).ObterDtoAsync(TestContext.Current.CancellationToken);

        Assert.Equal((3, 3, 30, 7, 15, 15),
            (o.FaltasConsecutivas, o.FaltasJanela, o.JanelaDias, o.DiasSemAcolhimento, o.DiasSemAtualizacao, o.DiasPeiPendente));
    }

    // A mudança fica numa transação revertida: o banco compartilhado não é alterado.
    [Fact]
    public async Task Painel_usa_os_limites_do_banco()
    {
        var ct = TestContext.Current.CancellationToken;
        var hoje = EncaminhamentoQueriesTests.Hoje;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        await EncaminhamentoQueriesTests.SalvarAsync(fx, aluno.Id, curso, prof, DestinoEncaminhamento.Externo, hoje.AddDays(-5));

        await using var db = fx.CriarContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var config = new ConfiguracaoAlertasRepositorio(db);
        var painel = new PainelQueries(db, SqlServerFixture.Relogio());
        var escopo = EscopoEncaminhamentos.Para(new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorNap));

        Task<int> Alertas(Nap.Application.Features.Configuracoes.ConfiguracaoAlertasDto o) =>
            painel.ObterAlertasEncaminhamentoAsync(escopo, prof, false, hoje, o, ct).ContinueWith(t => t.Result.Count, ct);

        Assert.Equal(0, await Alertas(await config.ObterDtoAsync(ct)));

        await db.ConfiguracoesAlertas.ExecuteUpdateAsync(s => s.SetProperty(c => c.DiasSemAcolhimento, 3), ct);
        var alterada = await config.ObterDtoAsync(ct);
        Assert.Equal(3, alterada.DiasSemAcolhimento);
        Assert.Equal(1, await Alertas(alterada));

        await tx.RollbackAsync(ct);
    }

    // Persiste de verdade (rowversion, auditoria e SaveChanges reais) e desfaz no fim; transação externa mantém o banco compartilhado limpo.
    [Fact]
    public async Task Atualizar_persiste_com_versao_e_rejeita_versao_antiga()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fx.CriarContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var usuario = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.Administrador);
        var relogio = SqlServerFixture.Relogio();
        var repo = new ConfiguracaoAlertasRepositorio(db);
        var handler = new AtualizarConfiguracaoAlertasHandler(new AtualizarConfiguracaoAlertasValidator(), repo, usuario,
            AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo());
        var inicial = await repo.ObterDtoAsync(ct);

        var salvo = await handler.Handle(new AtualizarConfiguracaoAlertasCommand(4, 5, 60, 10, 20, 12, inicial.Versao), ct);

        Assert.Equal((4, 5, 60, 10, 20, 12), (salvo.FaltasConsecutivas, salvo.FaltasJanela, salvo.JanelaDias, salvo.DiasSemAcolhimento,
            salvo.DiasSemAtualizacao, salvo.DiasPeiPendente));
        Assert.NotEqual(inicial.Versao, salvo.Versao);
        Assert.Equal(20, (await repo.ObterDtoAsync(ct)).DiasSemAtualizacao);
        Assert.True(await db.EventosAuditoria.AnyAsync(a => a.Acao == "configuracao.alertas", ct));
        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new AtualizarConfiguracaoAlertasCommand(5, 5, 60, 10, 20, 12, inicial.Versao), ct));

        await tx.RollbackAsync(ct);
    }
}
