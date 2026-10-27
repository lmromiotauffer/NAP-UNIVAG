using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Domain.Agenda;
using Nap.Domain.Atividades;
using Nap.Domain.Comum.ValueObjects;
using Nap.Infrastructure.Persistencia.Consultas;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class AtividadeRepositoryTests(SqlServerFixture fx)
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Round_trip_com_correcoes()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var outroCurso = await fx.CriarCursoAsync();
        var inicio = Agora.AddHours(-3);
        var atividade = AtividadeCurso.RegistrarAvulsa(prof, IntervaloHorario.Criar(inicio, inicio.AddHours(1)), curso,
            TipoAtividade.RodaConversa, "Roda de conversa", "Atividade fora da agenda", prof, Agora);
        atividade.Corrigir(IntervaloHorario.Criar(inicio, inicio.AddMinutes(90)), outroCurso, TipoAtividade.Outra,
            "Descrição corrigida", "Ajuste de duração e curso", prof, Agora);

        await using (var db = fx.CriarContexto())
        {
            new AtividadeRepository(db).Adicionar(atividade);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var lida = await new AtividadeRepository(leitura).ObterAsync(atividade.Id, ct);

        Assert.NotNull(lida);
        Assert.Equal(outroCurso, lida.CursoId);
        Assert.Equal(inicio, lida.Intervalo.Inicio);
        Assert.Equal(TipoAtividade.Outra, lida.Tipo);
        Assert.Equal("Atividade fora da agenda", lida.JustificativaAvulso);
        var c = Assert.Single(lida.Correcoes);
        Assert.Equal(curso, c.CursoIdAnterior);
        Assert.Equal(TipoAtividade.RodaConversa, c.TipoAnterior);
        Assert.Equal("Roda de conversa", c.DescricaoAnterior);
        Assert.Equal(inicio.AddHours(1), c.FimAnterior);

        // A coordenação do curso atual não vê o estado anterior, que era de outro curso.
        var consultas = new AtividadeQueries(leitura, SqlServerFixture.Relogio());
        Assert.Single((await consultas.ObterAsync(atividade.Id, null, ct))!.Correcoes);
        Assert.Empty((await consultas.ObterAsync(atividade.Id, outroCurso, ct))!.Correcoes);
    }

    [Fact]
    public async Task Segunda_atividade_para_o_mesmo_compromisso_viola_indice()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var compromisso = Compromisso.Criar(TipoCompromisso.AtividadeCurso, "Roda",
            IntervaloHorario.Criar(Agora.AddHours(-2), Agora.AddHours(-1)), prof, null, null, prof);
        await using (var db = fx.CriarContexto())
        {
            db.Compromissos.Add(compromisso);
            await db.SaveChangesAsync(ct);
        }

        async Task RegistrarAsync(Persistencia.NapDbContext db)
        {
            var c = await db.Compromissos.AsNoTracking().SingleAsync(x => x.Id == compromisso.Id, ct);
            new AtividadeRepository(db).Adicionar(AtividadeCurso.RegistrarDeCompromisso(c, c.Intervalo, null,
                TipoAtividade.RodaConversa, "Roda de conversa", prof, Agora));
            await new Persistencia.UnidadeDeTrabalho(db).SalvarAsync(ct);
        }

        await using var a = fx.CriarContexto();
        await using var b = fx.CriarContexto();
        await RegistrarAsync(a);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => RegistrarAsync(b));

        Assert.Equal("Este agendamento já possui atividade registrada.", ex.Message);
        Assert.True(await new AtividadeRepository(b).ExisteParaCompromissoAsync(compromisso.Id, ct));
    }

    [Fact]
    public async Task Institucional_persiste_sem_curso()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var inicio = Agora.AddHours(-3);
        var atividade = AtividadeCurso.RegistrarAvulsa(prof, IntervaloHorario.Criar(inicio, inicio.AddHours(2)), null,
            TipoAtividade.ReuniaoPedagogica, "Reunião institucional", "Sem agendamento prévio", prof, Agora);
        await using (var db = fx.CriarContexto())
        {
            new AtividadeRepository(db).Adicionar(atividade);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var lida = await new AtividadeRepository(leitura).ObterAsync(atividade.Id, ct);

        Assert.NotNull(lida);
        Assert.Null(lida.CursoId);
        Assert.Null(lida.CompromissoId);
    }
}
