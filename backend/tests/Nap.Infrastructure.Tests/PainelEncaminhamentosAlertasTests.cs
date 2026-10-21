using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Painel;
using Nap.Domain.Comum;
using Nap.Domain.Encaminhamentos;
using Nap.Infrastructure.Identidade;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Tests;

// Mesma coleção dos testes que criam usuários Sip (Outbox): rodam em série com este. Além disso, as mudanças em usuários
// ficam numa transação revertida, então o banco compartilhado não é alterado.
[Collection("Outbox")]
public class PainelEncaminhamentosAlertasTests(SqlServerFixture fx)
{
    private static readonly DateOnly Hoje = EncaminhamentoQueriesTests.Hoje;

    private static EscopoEncaminhamentos Equipe() =>
        EscopoEncaminhamentos.Para(new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorNap));

    [Fact]
    public async Task Alerta_sem_destinatario_de_destino_sip_depende_de_usuario_sip_ativo()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        await EncaminhamentoQueriesTests.SalvarAsync(fx, aluno.Id, curso, prof, DestinoEncaminhamento.Sip);
        await EncaminhamentoQueriesTests.SalvarAsync(fx, aluno.Id, curso, prof, DestinoEncaminhamento.ClinicaEscola);
        await EncaminhamentoQueriesTests.SalvarAsync(fx, aluno.Id, curso, prof, DestinoEncaminhamento.Externo);

        await using var db = fx.CriarContexto();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Users.Where(u => u.Perfil == Perfis.Sip).ExecuteUpdateAsync(s => s.SetProperty(u => u.Ativo, false), ct);
        var painel = new PainelQueries(db, SqlServerFixture.Relogio());

        async Task<int> SemDestinatario() =>
            (await painel.ObterAlertasEncaminhamentoAsync(Equipe(), prof, true, Hoje, SqlServerFixture.AlertasPadrao, ct))
            .Count(a => a.Tipo == "encaminhamento-sem-destinatario");

        // Sem Sip ativo: Sip e Clínica Escola alertam (Externo nunca). Se o ramo Sip do predicado for negado ou removido, falha.
        Assert.Equal(2, await SemDestinatario());

        var id = Guid.NewGuid();
        db.Users.Add(new UsuarioIdentidade { Id = id, UserName = id.ToString("N"), NomeCompleto = "Sip ativo", Perfil = Perfis.Sip, Ativo = true });
        await db.SaveChangesAsync(ct);
        Assert.Equal(0, await SemDestinatario());

        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Prazo_sem_acolhimento_so_alerta_a_partir_de_oito_dias()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        await EncaminhamentoQueriesTests.SalvarAsync(fx, aluno.Id, curso, prof, DestinoEncaminhamento.Externo, Hoje.AddDays(-7));

        await using var db = fx.CriarContexto();
        var painel = new PainelQueries(db, SqlServerFixture.Relogio());
        var opcoes = SqlServerFixture.AlertasPadrao;
        Task<IReadOnlyList<AlertaDto>> Alertas() => painel.ObterAlertasEncaminhamentoAsync(Equipe(), prof, false, Hoje, opcoes, ct);

        Assert.Empty(await Alertas());
        Assert.Equal(0, (await painel.ContarEncaminhamentosAsync(Equipe(), prof, Hoje, opcoes, ct)).SemAcolhimentoNoPrazo);

        await EncaminhamentoQueriesTests.SalvarAsync(fx, aluno.Id, curso, prof, DestinoEncaminhamento.Externo, Hoje.AddDays(-8));
        Assert.Equal(8, Assert.Single(await Alertas()).DiasOuOcorrencias);
        Assert.Equal(1, (await painel.ContarEncaminhamentosAsync(Equipe(), prof, Hoje, opcoes, ct)).SemAcolhimentoNoPrazo);
    }
}
