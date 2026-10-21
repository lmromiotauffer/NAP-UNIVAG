using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Atividades;
using Nap.Domain.Comum.ValueObjects;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Tests;

public class AgendaAvulsosTests(SqlServerFixture fx)
{
    private static DateTimeOffset Local(int dia, int hora) => new(2026, 9, dia, hora, 0, 0, TimeSpan.FromHours(-4));

    [Fact]
    public async Task Semana_lista_avulsos_realizados_e_ausentes_da_janela_e_do_profissional()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional avulsos");
        var outro = await fx.CriarProfissionalAsync(nome: "Outro profissional");
        var curso = await fx.CriarCursoAsync("Curso avulsos");
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof, "Aluno avulsos");
        var agora = Local(30, 12);

        Atendimento Avulso(Guid profissional, EstadoAtendimento estado, DateTimeOffset inicio)
        {
            var intervalo = IntervaloHorario.Criar(inicio, inicio.AddMinutes(50));
            var cancelado = estado == EstadoAtendimento.Cancelado;
            var a = Atendimento.RegistrarAvulso(aluno.Id, profissional, intervalo, TipoAtendimento.Acompanhamento,
                cancelado ? EstadoAtendimento.Ausente : estado,
                estado == EstadoAtendimento.Realizado ? new DadosClinicos("Demanda", "Privado", null, null) : null,
                "Justificativa avulso", profissional, agora);
            // O registro só nasce Realizado/Ausente; Cancelado vem de correção.
            if (cancelado) a.Corrigir(EstadoAtendimento.Cancelado, TipoAtendimento.Acompanhamento, intervalo, null, null, profissional, true, agora);
            return a;
        }

        var dentro = Avulso(prof, EstadoAtendimento.Realizado, Local(15, 10));
        var ausente = Avulso(prof, EstadoAtendimento.Ausente, Local(16, 9));
        await using (var db = fx.CriarContexto())
        {
            db.AddRange(dentro, ausente,
                Avulso(prof, EstadoAtendimento.Realizado, Local(22, 10)), // outra semana
                Avulso(prof, EstadoAtendimento.Cancelado, Local(17, 10)), // cancelado não ocupa horário
                Avulso(outro, EstadoAtendimento.Realizado, Local(18, 10)));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var queries = new AgendaQueries(leitura, SqlServerFixture.Relogio());
        var segunda = new DateOnly(2026, 9, 14);

        var semana = await queries.ListarSemanaAsync(segunda, prof, ct);
        Assert.Equal([dentro.Id, ausente.Id], semana.AtendimentosAvulsos.Select(a => a.Id));
        var primeiro = semana.AtendimentosAvulsos[0];
        Assert.Equal(("Profissional avulsos", "Aluno avulsos", EstadoAtendimento.Realizado, Local(15, 10), Local(15, 10).AddMinutes(50)),
            (primeiro.Profissional, primeiro.Aluno, primeiro.Estado, primeiro.Inicio, primeiro.Fim));

        var todos = await queries.ListarSemanaAsync(segunda, null, ct);
        Assert.Contains(todos.AtendimentosAvulsos, a => a.ProfissionalId == outro);
        Assert.DoesNotContain(todos.AtendimentosAvulsos, a => a.Estado == EstadoAtendimento.Cancelado);

        var proxima = await queries.ListarSemanaAsync(segunda.AddDays(7), prof, ct);
        Assert.Single(proxima.AtendimentosAvulsos);
    }

    [Fact]
    public async Task Semana_lista_atividades_avulsas_e_mostra_o_horario_da_atividade_no_compromisso_concluido()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional atividades");
        var outro = await fx.CriarProfissionalAsync();
        var agora = Local(30, 12);
        AtividadeCurso Avulsa(Guid profissional, DateTimeOffset inicio) => AtividadeCurso.RegistrarAvulsa(profissional,
            IntervaloHorario.Criar(inicio, inicio.AddMinutes(60)), null, TipoAtividade.RodaConversa, "Descrição reservada",
            "Justificativa avulsa", profissional, agora);

        var dentro = Avulsa(prof, Local(15, 10));
        var compromisso = Compromisso.Criar(TipoCompromisso.AtividadeCurso, "Roda", IntervaloHorario.Criar(Local(16, 9), Local(16, 10)),
            prof, null, null, prof, agora);
        // O registro ajusta o intervalo (11h–12h) depois do agendado (9h–10h).
        var vinculada = AtividadeCurso.RegistrarDeCompromisso(compromisso, IntervaloHorario.Criar(Local(16, 11), Local(16, 12)), null,
            TipoAtividade.RodaConversa, "Roda de conversa", prof, agora);
        await using (var db = fx.CriarContexto())
        {
            db.AddRange(dentro, compromisso, vinculada, Avulsa(prof, Local(22, 10)), Avulsa(outro, Local(18, 10)));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var semana = await new AgendaQueries(leitura, SqlServerFixture.Relogio()).ListarSemanaAsync(new DateOnly(2026, 9, 14), prof, ct);

        var atividade = Assert.Single(semana.AtividadesAvulsas);
        Assert.Equal((dentro.Id, "Profissional atividades", TipoAtividade.RodaConversa, Local(15, 10), Local(15, 11)),
            (atividade.Id, atividade.Profissional, atividade.Tipo, atividade.Inicio, atividade.Fim));
        var concluido = Assert.Single(semana.Compromissos);
        Assert.Equal((EstadoCompromisso.Concluido, Local(16, 11), Local(16, 12)), (concluido.Estado, concluido.Inicio, concluido.Fim));

        var todos = await new AgendaQueries(leitura, SqlServerFixture.Relogio()).ListarSemanaAsync(new DateOnly(2026, 9, 14), null, ct);
        Assert.Contains(todos.AtividadesAvulsas, a => a.ProfissionalId == outro);
    }
}
