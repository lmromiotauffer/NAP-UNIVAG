using Nap.Application.Features.Agenda;
using Nap.Domain.Agenda;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class AutorizacaoForaEscalaPersistenciaTests(SqlServerFixture fx)
{
    private static readonly TimeSpan Cuiaba = TimeSpan.FromHours(-4);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Segunda-feira distante e aleatória: o banco é compartilhado e feriados ocupam a data para todos.</summary>
    private static DateOnly SegundaAleatoria()
    {
        var d = new DateOnly(2300, 1, 1).AddDays(7 * Random.Shared.Next(0, 20000));
        return d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
    }

    private static DateTimeOffset Em(DateOnly data, int hora) => new(data.ToDateTime(new TimeOnly(hora, 0)), Cuiaba);

    private static ServicoEscala Servico(NapDbContext db)
    {
        var relogio = SqlServerFixture.Relogio();
        return new ServicoEscala(new EscalaRepository(db), new FeriadoRepository(db, relogio), relogio);
    }

    [Fact]
    public async Task Autorizacao_ativa_vale_para_o_profissional_inclusive_em_feriado_e_a_revogada_volta_a_recusar()
    {
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional Autorizado");
        var outro = await fx.CriarProfissionalAsync(nome: "Outro Profissional");
        var segunda = SegundaAleatoria();
        var feriado = segunda.AddDays(1);
        var agora = DateTimeOffset.UtcNow;
        Guid revogavel;
        await using (var db = fx.CriarContexto())
        {
            db.Feriados.Add(Feriado.Criar(feriado, "Feriado da autorização"));
            db.AutorizacoesForaEscala.Add(AutorizacaoForaEscala.Criar(prof, IntervaloHorario.Criar(Em(segunda, 19), Em(segunda, 22)), "Plantão noturno", outro, agora));
            db.AutorizacoesForaEscala.Add(AutorizacaoForaEscala.Criar(prof, IntervaloHorario.Criar(Em(feriado, 8), Em(feriado, 12)), "Plantão no feriado", outro, agora));
            var a = AutorizacaoForaEscala.Criar(prof, IntervaloHorario.Criar(Em(segunda.AddDays(7), 19), Em(segunda.AddDays(7), 22)), "Será revogada", outro, agora);
            db.AutorizacoesForaEscala.Add(a);
            revogavel = a.Id;
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = fx.CriarContexto())
        {
            var servico = Servico(db);
            await servico.ExigirDentroDaEscalaAsync(prof, Em(segunda, 20), Ct);
            await servico.ExigirDentroDaEscalaAsync(prof, Em(feriado, 9), Ct);
            await servico.ExigirDentroDaEscalaAsync(prof, Em(segunda.AddDays(7), 20), Ct);
            await Assert.ThrowsAsync<DomainException>(() => servico.ExigirDentroDaEscalaAsync(prof, Em(segunda, 22), Ct));
            await Assert.ThrowsAsync<DomainException>(() => servico.ExigirDentroDaEscalaAsync(outro, Em(segunda, 20), Ct));
            await Assert.ThrowsAsync<DomainException>(() => servico.ExigirDentroDaEscalaAsync(outro, Em(feriado, 9), Ct));
        }

        await using (var db = fx.CriarContexto())
        {
            var repo = new EscalaRepository(db);
            (await repo.ObterAutorizacaoAsync(revogavel, Ct))!.Revogar(agora);
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = fx.CriarContexto())
        {
            await Assert.ThrowsAsync<DomainException>(() => Servico(db).ExigirDentroDaEscalaAsync(prof, Em(segunda.AddDays(7), 20), Ct));
            var pagina = await new EscalaRepository(db).ListarAutorizacoesAsync(prof, null, null, 1, 10, Ct);
            Assert.Equal(3, pagina.Total);
            Assert.Equal(["Profissional Autorizado"], pagina.Itens.Select(i => i.Profissional).Distinct());
            Assert.Single(pagina.Itens, i => i.RevogadaEm is not null);
            var soFeriado = await new EscalaRepository(db).ListarAutorizacoesAsync(prof, Em(feriado, 0), Em(feriado, 0).AddDays(1), 1, 10, Ct);
            Assert.Equal("Plantão no feriado", Assert.Single(soFeriado.Itens).Justificativa);
        }
    }
}
