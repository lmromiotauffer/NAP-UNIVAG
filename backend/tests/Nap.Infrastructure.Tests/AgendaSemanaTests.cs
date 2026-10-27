using Nap.Application.Comum;
using Nap.Domain.Agenda;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Tests;

public class AgendaSemanaTests(SqlServerFixture fx)
{
    private static DateTimeOffset Local(int dia, int hora) => new(2026, 8, dia, hora, 0, 0, TimeSpan.FromHours(-4));

    [Fact]
    public async Task Semana_traz_descricao_e_vinculo_de_remarcacao()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional remarcação");
        var agora = Local(1, 8);
        var original = Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião", IntervaloHorario.Criar(Local(3, 9), Local(3, 10)),
            prof, null, "Pauta: PEI do semestre", prof);
        var novo = original.Remarcar(IntervaloHorario.Criar(Local(4, 9), Local(4, 10)), prof);
        var simples = Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Sem descrição", IntervaloHorario.Criar(Local(5, 9), Local(5, 10)),
            prof, null, null, prof);
        await using (var db = fx.CriarContexto())
        {
            db.AddRange(original, novo, simples);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var semana = await new AgendaQueries(leitura, SqlServerFixture.Relogio()).ListarSemanaAsync(new DateOnly(2026, 8, 3), prof, ct);

        // O Remarcado sai da listagem; o novo traz a descrição herdada e o vínculo com o original.
        Assert.Equal([novo.Id, simples.Id], semana.Compromissos.Select(c => c.Id));
        var remarcado = semana.Compromissos[0];
        Assert.Equal(("Pauta: PEI do semestre", original.Id), (remarcado.Descricao, remarcado.RemarcadoDeId));
        Assert.Null(semana.Compromissos[1].Descricao);

        var gravado = await leitura.Compromissos.FindAsync([original.Id], ct);
        Assert.Equal(novo.Id, gravado!.RemarcadoParaId);
    }

    [Fact]
    public async Task Semana_marca_quem_pode_gerir_o_grupo_criador_coordenacao_e_nao_participante()
    {
        var ct = TestContext.Current.CancellationToken;
        var criador = await fx.CriarProfissionalAsync(nome: "Criador do grupo");
        var colega = await fx.CriarProfissionalAsync(nome: "Colega do grupo");
        var coordenador = await fx.CriarProfissionalAsync(Perfis.CoordenadorNap, "Coordenação do grupo");
        var agora = Local(1, 8);
        var grupo = Guid.NewGuid();
        // O colega remarca a própria instância: o autor da remarcação não vira criador do grupo.
        var doCriador = Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião do grupo", IntervaloHorario.Criar(Local(18, 9), Local(18, 10)),
            criador, null, null, criador, grupo);
        var doColega = Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião do grupo", IntervaloHorario.Criar(Local(18, 9), Local(18, 10)),
            colega, null, null, criador, grupo);
        var remarcada = doColega.Remarcar(IntervaloHorario.Criar(Local(19, 9), Local(19, 10)), colega);
        var individual = Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Individual", IntervaloHorario.Criar(Local(20, 9), Local(20, 10)),
            criador, null, null, criador);
        await using (var db = fx.CriarContexto())
        {
            db.AddRange(doCriador, doColega, remarcada, individual);
            await db.SaveChangesAsync(ct);
        }

        async Task<Dictionary<Guid, bool>> Ver(Guid quem, string perfil)
        {
            await using var leitura = fx.CriarContexto();
            var semana = await new AgendaQueries(leitura, SqlServerFixture.Relogio(), new UsuarioAtualTeste(quem, perfil))
                .ListarSemanaAsync(new DateOnly(2026, 8, 17), null, ct);
            return semana.Compromissos.Where(c => c.Id == doCriador.Id || c.Id == remarcada.Id || c.Id == individual.Id)
                .ToDictionary(c => c.Id, c => c.PodeGerirGrupo);
        }

        Assert.Equal(new() { [doCriador.Id] = true, [remarcada.Id] = true, [individual.Id] = false }, await Ver(criador, Perfis.ProfissionalNap));
        Assert.Equal(new() { [doCriador.Id] = false, [remarcada.Id] = false, [individual.Id] = false }, await Ver(colega, Perfis.ProfissionalNap));
        Assert.Equal(new() { [doCriador.Id] = true, [remarcada.Id] = true, [individual.Id] = false }, await Ver(coordenador, Perfis.CoordenadorNap));
    }
}
