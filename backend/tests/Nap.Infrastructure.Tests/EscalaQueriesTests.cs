using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Agenda.Commands;
using Nap.Application.Features.Cursos.Commands;
using Nap.Application.Features.Producao;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Atividades;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Consultas;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class EscalaQueriesTests(SqlServerFixture fx)
{
    private static readonly TimeSpan Cuiaba = TimeSpan.FromHours(-4);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Segunda-feira distante e aleatória: o banco é compartilhado e feriados ocupam a data para todos.</summary>
    private static DateOnly SegundaAleatoria()
    {
        var d = new DateOnly(2300, 1, 1).AddDays(7 * Random.Shared.Next(0, 20000));
        return d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
    }

    private static DateTimeOffset Em(DateOnly data, int hora, int minuto = 0) => new(data.ToDateTime(new TimeOnly(hora, minuto)), Cuiaba);

    private static ServicoEscala Servico(NapDbContext db)
    {
        var relogio = SqlServerFixture.Relogio();
        return new ServicoEscala(new EscalaRepository(db), new FeriadoRepository(db, relogio), relogio);
    }

    [Fact]
    public async Task Escala_vigente_respeita_a_vigencia_e_lista_com_versao()
    {
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional Escala");
        var segunda = SegundaAleatoria();
        await using (var db = fx.CriarContexto())
        {
            db.EscalasProfissional.Add(EscalaProfissional.Criar(prof, segunda, segunda.AddDays(1),
                [new FaixaEscala(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(12, 0), false),
                 new FaixaEscala(DayOfWeek.Monday, new TimeOnly(13, 0), new TimeOnly(18, 0), true)]));
            db.EscalasProfissional.Add(EscalaProfissional.Criar(prof, segunda.AddDays(2), null,
                [new FaixaEscala(DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(12, 0), false)]));
            await db.SaveChangesAsync(Ct);
        }

        await using var leitura = fx.CriarContexto();
        var servico = Servico(leitura);
        Assert.Equal(new TimeOnly(13, 0), (await servico.FaixaVigenteAsync(prof, segunda, Ct))!.Inicio);
        Assert.Null(await servico.FaixaVigenteAsync(prof, segunda.AddDays(2), Ct)); // quarta: a nova escala não tem
        Assert.NotNull(await servico.FaixaVigenteAsync(prof, segunda.AddDays(5), Ct));
        // Antes da primeira vigência, vale a padrão.
        Assert.Equal(new TimeOnly(7, 0), (await servico.FaixaVigenteAsync(prof, segunda.AddDays(-7), Ct))!.Inicio);

        // O instante é convertido para o fuso institucional (faixa de segunda: 13–18h): 18:00Z = 14:00 em Cuiabá, dentro;
        // 22:30Z = 18:30, fora.
        await servico.ExigirDentroDaEscalaAsync(prof, Em(segunda, 13), Ct);
        await servico.ExigirDentroDaEscalaAsync(prof, new DateTimeOffset(segunda.ToDateTime(new TimeOnly(18, 0)), TimeSpan.Zero), Ct);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            servico.ExigirDentroDaEscalaAsync(prof, new DateTimeOffset(segunda.ToDateTime(new TimeOnly(22, 30)), TimeSpan.Zero), Ct));
        Assert.Equal("Fora da escala do profissional neste dia.", ex.Message);

        var lista = await new EscalaRepository(leitura).ListarAsync(prof, Ct);
        Assert.Equal(2, lista.Count);
        Assert.Equal(segunda.AddDays(2), lista[0].VigenciaInicio); // mais recente primeiro
        Assert.Equal("Profissional Escala", lista[1].Profissional);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday], lista[1].Faixas.Select(f => f.Dia));
        Assert.False(string.IsNullOrEmpty(lista[1].Versao));
    }

    [Fact]
    public async Task Escalas_simultaneas_sobrepostas_do_mesmo_profissional_gravam_so_uma()
    {
        var prof = await fx.CriarProfissionalAsync();
        var segunda = SegundaAleatoria();
        var tarefas = Enumerable.Range(0, 5).Select(i => Task.Run(async () =>
        {
            await using var db = fx.CriarContexto();
            var usuario = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorNap);
            var relogio = SqlServerFixture.Relogio();
            var handler = new CriarEscalaHandler(new CriarEscalaValidator(), new EscalaRepository(db), new AgendaRepository(db),
                new UsuarioQueries(db), usuario, AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db),
                new ServicoFechamento(new FechamentoRepository(db), relogio));
            return await handler.Handle(new CriarEscalaCommand(prof, segunda.AddDays(i), null,
                [new FaixaEscalaInput(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0), false)]), Ct);
        }, Ct)).ToList();
        try { await Task.WhenAll(tarefas); } catch { /* avaliado abaixo */ }

        Assert.Equal(1, tarefas.Count(t => t.IsCompletedSuccessfully));
        Assert.All(tarefas.Where(t => t.IsFaulted), t =>
            Assert.Equal(CriarEscalaHandler.MensagemSobreposicao, Assert.IsType<ConflictException>(t.Exception!.InnerException).Message));
        await using var leitura = fx.CriarContexto();
        Assert.Equal(1, await leitura.EscalasProfissional.CountAsync(e => e.ProfissionalId == prof, Ct));
    }

    [Fact]
    public async Task Feriado_lista_conflitos_do_dia_no_fuso_institucional()
    {
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional Feriado");
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, await fx.CriarCursoAsync(), prof);
        var dia = SegundaAleatoria().AddDays(2);
        Compromisso Novo(TipoCompromisso tipo, DateTimeOffset inicio) =>
            Compromisso.Criar(tipo, tipo.ToString(), IntervaloHorario.Criar(inicio, inicio.AddMinutes(30)), prof, null, null, prof);

        var agendado = Novo(TipoCompromisso.ReuniaoEquipe, Em(dia, 23, 15)); // 03:15Z do dia seguinte: ainda é o dia em Cuiabá
        var lembrete = Novo(TipoCompromisso.Lembrete, Em(dia, 10));
        var cancelado = Novo(TipoCompromisso.Bloqueio, Em(dia, 11));
        cancelado.Cancelar("Imprevisto");
        var diaSeguinte = Novo(TipoCompromisso.ReuniaoEquipe, Em(dia.AddDays(1), 0, 15));
        var atendimento = Atendimento.RegistrarAvulso(aluno.Id, prof, IntervaloHorario.Criar(Em(dia, 9), Em(dia, 10)),
            TipoAtendimento.Acompanhamento, EstadoAtendimento.Ausente, new DadosClinicos("Sigiloso", "Privado", null, null),
            "Sem agenda", prof, Em(dia, 12));
        var realizada = Novo(TipoCompromisso.ReuniaoEquipe, Em(dia, 13));
        realizada.RegistrarRealizacao(IntervaloHorario.Criar(Em(dia, 13), Em(dia, 13, 30)), prof, Em(dia, 14));
        var atividade = AtividadeCurso.RegistrarAvulsa(prof, IntervaloHorario.Criar(Em(dia, 14), Em(dia, 15)), null,
            TipoAtividade.RodaConversa, "Roda de conversa", "Sem agenda", prof, Em(dia, 16));
        await using (var db = fx.CriarContexto())
        {
            db.Compromissos.AddRange(agendado, lembrete, cancelado, diaSeguinte, realizada);
            db.Atendimentos.Add(atendimento);
            db.AtividadesCurso.Add(atividade);
            await db.SaveChangesAsync(Ct);
        }

        await using var escrita = fx.CriarContexto();
        var usuario = new UsuarioAtualTeste(prof, Perfis.CoordenadorNap);
        var relogio = SqlServerFixture.Relogio();
        var handler = new CriarFeriadoHandler(new CriarFeriadoValidator(), new FeriadoRepository(escrita, relogio), usuario,
            AuditoriaTeste.Criar(escrita, usuario, relogio), new UnidadeDeTrabalho(escrita),
            new ServicoFechamento(new FechamentoRepository(escrita), relogio));
        var criado = await handler.Handle(new CriarFeriadoCommand(dia, "Feriado municipal"), Ct);

        var meus = criado.Conflitos.Where(c => c.ProfissionalId == prof).ToList();
        Assert.Equal([(atendimento.Id, "Atendimento"), (realizada.Id, "Realização"), (atividade.Id, "Atividade"), (agendado.Id, "Compromisso")],
            meus.Select(c => (c.Id, c.Tipo)));
        Assert.All(meus, c => Assert.Equal("Profissional Feriado", c.Profissional));
        Assert.DoesNotContain(meus, c => c.Titulo.Contains("Sigiloso"));

        // Nada foi alterado, e a data agora rejeita novos agendamentos.
        await using var leitura = fx.CriarContexto();
        Assert.Equal(EstadoCompromisso.Agendado, (await leitura.Compromissos.SingleAsync(c => c.Id == agendado.Id, Ct)).Estado);
        Assert.Equal(EstadoAtendimento.Ausente, (await leitura.Atendimentos.SingleAsync(a => a.Id == atendimento.Id, Ct)).Estado);
        var ex = await Assert.ThrowsAsync<DomainException>(() => Servico(leitura).ExigirDentroDaEscalaAsync(prof, Em(dia, 9), Ct));
        Assert.Equal("Esta data é feriado: Feriado municipal.", ex.Message);
    }

    [Fact]
    public async Task Feriado_ativo_simultaneo_na_mesma_data_retorna_conflito_legivel()
    {
        var dia = SegundaAleatoria();
        await using (var db = fx.CriarContexto())
        {
            db.Feriados.Add(Feriado.Criar(dia, "Primeiro"));
            await db.SaveChangesAsync(Ct);
        }
        // Simula a corrida: a pré-checagem passou, o índice único barra na gravação.
        await using var outro = fx.CriarContexto();
        outro.Feriados.Add(Feriado.Criar(dia, "Segundo"));
        var ex = await Assert.ThrowsAsync<ConflictException>(() => new UnidadeDeTrabalho(outro).SalvarAsync(Ct));
        Assert.Equal(CriarFeriadoHandler.MensagemDuplicado, ex.Message);
    }

    [Fact]
    public async Task Curso_tem_versao_nome_sem_caixa_e_inativo_deixa_de_ser_atribuivel()
    {
        var nome = $"Curso {Guid.NewGuid():N}";
        var id = await fx.CriarCursoAsync(nome);
        await using var db = fx.CriarContexto();
        var repo = new CursoRepository(db);
        Assert.True(await repo.NomeExisteAsync(nome.ToUpperInvariant(), null, Ct));
        Assert.False(await repo.NomeExisteAsync(nome, id, Ct));

        var versao = (await new CursoQueries(db).ListarAsync(Ct)).Single(c => c.Id == id).Versao;
        var usuario = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.Administrador);
        var relogio = SqlServerFixture.Relogio();
        await new AlterarSituacaoCursoHandler(new AlterarSituacaoCursoValidator(), repo, usuario, AuditoriaTeste.Criar(db, usuario, relogio),
            new UnidadeDeTrabalho(db), new CacheNulo()).Handle(new AlterarSituacaoCursoCommand(id, false, versao), Ct);
        Assert.False(await new CursoQueries(db).CursoAtivoExisteAsync(id, Ct));

        // A versão lida antes da inativação ficou velha.
        await using var outro = fx.CriarContexto();
        var ex = await Assert.ThrowsAsync<ConflictException>(() => new RenomearCursoHandler(new RenomearCursoValidator(), new CursoRepository(outro),
            usuario, AuditoriaTeste.Criar(outro, usuario, relogio), new UnidadeDeTrabalho(outro), new CacheNulo())
            .Handle(new RenomearCursoCommand(id, nome + " novo", versao), Ct));
        Assert.Equal(UnidadeDeTrabalho.MensagemRegistroAlterado, ex.Message);
    }
}
