using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nap.Application.Comum;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Infrastructure.Identidade;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Consultas;
using Nap.Infrastructure.Seed;

namespace Nap.Infrastructure.Tests;

public class SemeadorTests(SqlServerFixture sql, RedisFixture redis)
{
    [Fact]
    public async Task Semeador_exige_senha_e_e_idempotente_com_dados_que_acionam_os_alertas()
    {
        var ct = TestContext.Current.CancellationToken;
        // Banco próprio: os dados de demonstração não podem contaminar as contagens dos outros testes.
        var conexao = new SqlConnectionStringBuilder(sql.ConnectionString) { InitialCatalog = "NapSemeador" }.ConnectionString;
        await using (var db = new NapDbContext(new DbContextOptionsBuilder<NapDbContext>().UseSqlServer(conexao).Options))
            await db.Database.MigrateAsync(ct);

        await using (var semSenha = RedisFixture.CriarProvedor(sql, redis, conexao, new Dictionary<string, string?> { ["Seed:SenhaPadrao"] = null }))
        await using (var escopo = semSenha.CreateAsyncScope())
        {
            var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => escopo.ServiceProvider.GetRequiredService<SemeadorDemonstracao>().ExecutarAsync(ct));
            Assert.Contains("Seed:SenhaPadrao", erro.Message);
        }

        await using var sp = RedisFixture.CriarProvedor(sql, redis, conexao);
        for (var i = 0; i < 2; i++)
        {
            await using var escopo = sp.CreateAsyncScope();
            await escopo.ServiceProvider.GetRequiredService<SemeadorDemonstracao>().ExecutarAsync(ct);
        }

        await using var leitura = new NapDbContext(new DbContextOptionsBuilder<NapDbContext>().UseSqlServer(conexao).Options);
        Assert.Equal(6, await leitura.Cursos.CountAsync(ct));
        Assert.Equal(9, await leitura.Users.CountAsync(u => u.Email!.EndsWith("@nap.demo"), ct));
        Assert.Equal(20, await leitura.Alunos.CountAsync(a => a.ObservacoesAcademicas == "Dado fictício de demonstração", ct));
        Assert.Equal(20, await leitura.Alunos.CountAsync(ct));
        var estados = await leitura.Atendimentos.GroupBy(a => a.Estado).Select(g => g.Key).ToListAsync(ct);
        Assert.Equal([EstadoAtendimento.Realizado, EstadoAtendimento.Ausente], estados.Order());
        Assert.True(await leitura.Compromissos.AnyAsync(c => c.Estado == EstadoCompromisso.Agendado, ct));

        var relogio = SqlServerFixture.Relogio();
        var alertas = await new PainelQueries(leitura, relogio).ObterAlertasAsync(relogio.Hoje, null, null, SqlServerFixture.AlertasPadrao, ct);
        Assert.Contains(alertas, a => a.Tipo == "faltas_recorrentes" && a.Detalhe.Contains("consecutivas"));
        Assert.Contains(alertas, a => a.Tipo == "sem_desfecho");

        await using var login = sp.CreateAsyncScope();
        var sessao = await login.ServiceProvider.GetRequiredService<ServicoSessao>().EntrarAsync("coord.direito@nap.demo", "SenhaDemo2026", ct);
        Assert.Equal("Direito", sessao.Usuario.Curso);
    }

    [Fact]
    public async Task Semeador_recusa_rodar_em_producao()
    {
        await using var sp = RedisFixture.CriarProvedor(sql, redis, ambiente: "Production");
        await using var escopo = sp.CreateAsyncScope();
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            escopo.ServiceProvider.GetRequiredService<SemeadorDemonstracao>().ExecutarAsync(TestContext.Current.CancellationToken));
        Assert.Contains("produção", erro.Message);
    }
}
