using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum.ValueObjects;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class AtendimentoIntegridadeTests(SqlServerFixture fx)
{
    [Fact]
    public async Task Segundo_atendimento_para_o_mesmo_compromisso_viola_indice_e_gera_conflito()
    {
        var ct = TestContext.Current.CancellationToken;
        var agora = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var prof = await fx.CriarProfissionalAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, await fx.CriarCursoAsync(), prof);
        var compromisso = Compromisso.Criar(TipoCompromisso.Atendimento, "Atendimento",
            IntervaloHorario.Criar(agora.AddHours(-2), agora.AddHours(-1)), prof, aluno.Id, null, prof);
        await using (var db = fx.CriarContexto())
        {
            db.Compromissos.Add(compromisso);
            await db.SaveChangesAsync(ct);
        }

        // Sem checagem prévia: cada contexto lê o compromisso sem rastreamento e grava só o atendimento.
        async Task RegistrarAsync(NapDbContext db)
        {
            var c = await db.Compromissos.AsNoTracking().SingleAsync(x => x.Id == compromisso.Id, ct);
            new AtendimentoRepository(db).Adicionar(Atendimento.RegistrarDeCompromisso(c, TipoAtendimento.Acompanhamento,
                EstadoAtendimento.Realizado, new DadosClinicos("Demanda", null, null, null), prof, agora));
            await new UnidadeDeTrabalho(db).SalvarAsync(ct);
        }

        await using var a = fx.CriarContexto();
        await using var b = fx.CriarContexto();
        await RegistrarAsync(a);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => RegistrarAsync(b));

        Assert.Equal("Este agendamento já possui atendimento registrado", ex.Message);
        Assert.True(await new AtendimentoRepository(b).ExisteParaCompromissoAsync(compromisso.Id, ct));
    }
}
