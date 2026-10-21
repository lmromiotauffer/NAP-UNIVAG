using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum.ValueObjects;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Tests;

public class VersaoTests(SqlServerFixture fx)
{
    [Fact]
    public async Task ExigirVersao_aceita_a_versao_lida_e_recusa_a_anterior_a_outra_edicao()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);

        string versaoLida;
        await using (var db = fx.CriarContexto())
            versaoLida = (await new AlunoQueries(db).ObterFichaAsync(aluno.Id, new EscopoAlunos(null, true), ct))!.Versao;
        Assert.NotEmpty(Convert.FromBase64String(versaoLida));

        await using (var db = fx.CriarContexto())
        {
            var a = (await db.Alunos.FindAsync([aluno.Id], ct))!;
            a.AtualizarDados("Outro nome", 4, null, null, prof);
            await db.SaveChangesAsync(ct);
        }

        string versaoAtual;
        await using (var db = fx.CriarContexto())
            versaoAtual = (await new AlunoQueries(db).ObterFichaAsync(aluno.Id, new EscopoAlunos(null, true), ct))!.Versao;

        await using var contexto = fx.CriarContexto();
        var uow = new UnidadeDeTrabalho(contexto);
        var carregado = (await contexto.Alunos.FindAsync([aluno.Id], ct))!;
        var e = Assert.Throws<ConflictException>(() => uow.ExigirVersao(carregado, versaoLida));
        Assert.Equal(UnidadeDeTrabalho.MensagemRegistroAlterado, e.Message);
        uow.ExigirVersao(carregado, versaoAtual);
        uow.ExigirVersao(carregado, $"\"{versaoAtual}\"");
    }

    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("não-é-base64")]
    public async Task ExigirVersao_com_valor_ilegivel_lanca_conflito(string versao)
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, await fx.CriarCursoAsync(), prof);

        await using var contexto = fx.CriarContexto();
        var carregado = (await contexto.Alunos.FindAsync([aluno.Id], ct))!;
        Assert.Throws<ConflictException>(() => new UnidadeDeTrabalho(contexto).ExigirVersao(carregado, versao));
    }

    [Fact]
    public async Task Ficha_compromisso_e_detalhe_de_atendimento_expoem_versao()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, await fx.CriarCursoAsync(), prof);
        var agora = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(-4));
        var inicio = agora.AddDays(2);
        var compromisso = Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião", IntervaloHorario.Criar(inicio, inicio.AddHours(1)),
            prof, null, null, prof, agora);
        var atendimento = Atendimento.RegistrarAvulso(aluno.Id, prof, IntervaloHorario.Criar(agora.AddDays(-1), agora.AddDays(-1).AddHours(1)),
            TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, new DadosClinicos("Demanda", "Obs", null, null),
            "Justificativa", prof, agora);
        await using (var db = fx.CriarContexto())
        {
            db.AddRange(compromisso, atendimento);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var ficha = (await new AlunoQueries(leitura).ObterFichaAsync(aluno.Id, new EscopoAlunos(null, true), ct))!;
        var semana = await new AgendaQueries(leitura, SqlServerFixture.Relogio()).ListarSemanaAsync(DateOnly.FromDateTime(inicio.Date), prof, ct);
        var detalhe = (await new AtendimentoQueries(leitura, SqlServerFixture.Relogio()).ObterAsync(atendimento.Id, ct))!;

        Assert.Equal(8, Convert.FromBase64String(ficha.Versao).Length);
        Assert.Equal(8, Convert.FromBase64String(Assert.Single(semana.Compromissos).Versao).Length);
        Assert.Equal(8, Convert.FromBase64String(detalhe.Versao).Length);
    }
}
