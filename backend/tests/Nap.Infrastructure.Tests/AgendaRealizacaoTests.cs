using Microsoft.EntityFrameworkCore;
using Nap.Domain.Agenda;
using Nap.Domain.Comum.ValueObjects;
using Nap.Infrastructure.Persistencia.Consultas;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class AgendaRealizacaoTests(SqlServerFixture fx)
{
    private static DateTimeOffset Local(int dia, int hora) => new(2026, 9, dia, hora, 0, 0, TimeSpan.FromHours(-4));
    private static readonly DateTimeOffset Agora = Local(30, 12);

    private async Task<(Guid Prof, Compromisso Reuniao)> CriarReuniaoRealizadaAsync(string nome)
    {
        var prof = await fx.CriarProfissionalAsync(nome: nome);
        var reuniao = Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião", IntervaloHorario.Criar(Local(16, 9), Local(16, 10)),
            prof, null, null, prof);
        // Realizada às 11h–12h, depois do agendado (9h–10h).
        reuniao.RegistrarRealizacao(IntervaloHorario.Criar(Local(16, 11), Local(16, 12)), prof, Agora);
        await using var db = fx.CriarContexto();
        db.Add(reuniao);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (prof, reuniao);
    }

    [Fact]
    public async Task Realizacao_ocupa_o_horario_efetivo()
    {
        var ct = TestContext.Current.CancellationToken;
        var (prof, reuniao) = await CriarReuniaoRealizadaAsync("Profissional realização");
        await using var db = fx.CriarContexto();
        var repo = new AgendaRepository(db);

        Assert.True(await repo.ExisteConflitoAsync(prof, null, IntervaloHorario.Criar(Local(16, 11).AddMinutes(30), Local(16, 13)), null, ct));
        Assert.False(await repo.ExisteConflitoAsync(prof, null, IntervaloHorario.Criar(Local(16, 9), Local(16, 10)), null, ct));
        Assert.False(await repo.ExisteConflitoAsync(prof, null, IntervaloHorario.Criar(Local(16, 12), Local(16, 13)), null, ct));
        Assert.False(await repo.ExisteConflitoAsync(prof, null, IntervaloHorario.Criar(Local(16, 11), Local(16, 12)), reuniao.Id, ct));
    }

    [Fact]
    public async Task Checagem_do_grupo_devolve_so_quem_conflita_e_respeita_os_ignorados()
    {
        var ct = TestContext.Current.CancellationToken;
        var (prof, reuniao) = await CriarReuniaoRealizadaAsync("Profissional grupo realização");
        var livre = await fx.CriarProfissionalAsync(nome: "Profissional grupo livre");
        var agendado = Compromisso.Criar(TipoCompromisso.Bloqueio, "Bloqueio", IntervaloHorario.Criar(Local(16, 11), Local(16, 12)),
            livre, null, null, livre);
        await using (var escrita = fx.CriarContexto())
        {
            escrita.Add(agendado);
            await escrita.SaveChangesAsync(ct);
        }
        await using var db = fx.CriarContexto();
        var repo = new AgendaRepository(db);
        var horario = IntervaloHorario.Criar(Local(16, 11), Local(16, 12));

        Assert.Equal(new[] { prof, livre }.Order(), (await repo.ProfissionaisComConflitoAsync([prof, livre], horario, [], ct)).Order());
        // Cada participante ignora a própria instância (remarcação do grupo).
        Assert.Equal([prof], await repo.ProfissionaisComConflitoAsync([prof, livre], horario, [agendado.Id], ct));
        Assert.Empty(await repo.ProfissionaisComConflitoAsync([prof, livre], horario, [agendado.Id, reuniao.Id], ct));
    }

    [Fact]
    public async Task Agenda_mostra_o_horario_efetivo_da_realizacao()
    {
        var ct = TestContext.Current.CancellationToken;
        var (prof, reuniao) = await CriarReuniaoRealizadaAsync("Profissional agenda realização");
        await using var leitura = fx.CriarContexto();

        var semana = await new AgendaQueries(leitura, SqlServerFixture.Relogio()).ListarSemanaAsync(new DateOnly(2026, 9, 14), prof, ct);
        var item = Assert.Single(semana.Compromissos);
        Assert.Equal((reuniao.Id, EstadoCompromisso.Concluido, Local(16, 11), Local(16, 12)), (item.Id, item.Estado, item.Inicio, item.Fim));
    }

    [Fact]
    public async Task Correcao_da_realizacao_grava_historico_e_ocupa_o_novo_horario()
    {
        var ct = TestContext.Current.CancellationToken;
        var (prof, reuniao) = await CriarReuniaoRealizadaAsync("Profissional correção realização");
        await using (var db = fx.CriarContexto())
        {
            var c = (await new AgendaRepository(db).ObterAsync(reuniao.Id, ct))!;
            c.CorrigirRealizacao(IntervaloHorario.Criar(Local(16, 14), Local(16, 15)), "Horário anotado errado", prof, Agora);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var correcao = Assert.Single(leitura.Set<CorrecaoRealizacao>().Where(x => EF.Property<Guid>(x, "CompromissoId") == reuniao.Id));
        Assert.Equal((Local(16, 11), Local(16, 12), "Horário anotado errado"), (correcao.InicioAnterior, correcao.FimAnterior, correcao.Justificativa));
        var repo = new AgendaRepository(leitura);
        Assert.True(await repo.ExisteConflitoAsync(prof, null, IntervaloHorario.Criar(Local(16, 14), Local(16, 15)), null, ct));
        Assert.False(await repo.ExisteConflitoAsync(prof, null, IntervaloHorario.Criar(Local(16, 11), Local(16, 12)), null, ct));
    }
}
