using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Agenda.Commands;
using Nap.Application.Features.Atividades;
using Nap.Application.Features.Atividades.Commands;
using Nap.Application.Features.Atividades.Queries;
using Nap.Application.Features.Atendimentos.Commands;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Atividades;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Infrastructure.Persistencia;
using Nap.Application.Features.Producao;
using Nap.Infrastructure.Persistencia.Consultas;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class AtividadeHandlersTests(SqlServerFixture fx)
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static RegistrarAtividadeHandler Registrador(NapDbContext db, IUsuarioAtual usuario, DateTimeOffset? agora = null)
    {
        var relogio = new RelogioTeste(agora ?? Agora);
        return new RegistrarAtividadeHandler(new RegistrarAtividadeValidator(), new AtividadeRepository(db), new AgendaRepository(db),
            new CursoQueries(db), new UsuarioQueries(db), usuario, relogio, AuditoriaTeste.Criar(db, usuario, relogio),
            new UnidadeDeTrabalho(db), new CacheNulo(), new ServicoFechamento(new FechamentoRepository(db), relogio),
            new ServicoEscala(new EscalaRepository(db), new FeriadoRepository(db, relogio), relogio));
    }

    [Fact]
    public async Task Registros_simultaneos_do_mesmo_compromisso_geram_uma_atividade()
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

        async Task RegistrarAsync()
        {
            await using var db = fx.CriarContexto();
            await Registrador(db, new UsuarioAtualTeste(prof)).Handle(new RegistrarAtividadeCommand(compromisso.Id, null,
                compromisso.Intervalo.Inicio, compromisso.Intervalo.Fim, null, TipoAtividade.RodaConversa, "Roda de conversa", null), ct);
        }

        var tarefas = Enumerable.Range(0, 8).Select(_ => Task.Run(RegistrarAsync)).ToList();
        try { await Task.WhenAll(tarefas); } catch { /* avaliado abaixo */ }

        Assert.Equal(1, tarefas.Count(t => t.IsCompletedSuccessfully));
        Assert.All(tarefas.Where(t => t.IsFaulted), t =>
            Assert.Equal("Este agendamento já possui atividade registrada.", Assert.IsType<ConflictException>(t.Exception!.InnerException).Message));
        await using var leitura = fx.CriarContexto();
        Assert.Equal(1, await leitura.AtividadesCurso.CountAsync(a => a.CompromissoId == compromisso.Id, ct));
        Assert.Equal(EstadoCompromisso.Concluido, (await leitura.Compromissos.SingleAsync(c => c.Id == compromisso.Id, ct)).Estado);
    }

    [Fact]
    public async Task Consultas_aplicam_recorte_do_curso_e_derivam_turno_e_data_no_fuso_institucional()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var outroCurso = await fx.CriarCursoAsync();
        // 22:30Z = 18:30 em Cuiabá (UTC-4): Noite, ainda no dia 1.
        var noite = new DateTimeOffset(2026, 9, 1, 22, 30, 0, TimeSpan.Zero);
        AtividadeCurso Nova(Guid? c, DateTimeOffset inicio) => AtividadeCurso.RegistrarAvulsa(prof,
            IntervaloHorario.Criar(inicio, inicio.AddMinutes(90)), c, TipoAtividade.RodaConversa, "Roda de conversa", "Sem agenda prévia", prof, Agora);
        var doCurso = Nova(curso, noite);
        var deOutro = Nova(outroCurso, noite.AddDays(1));
        var institucional = Nova(null, noite.AddDays(2));
        await using (var db = fx.CriarContexto())
        {
            foreach (var a in new[] { doCurso, deOutro, institucional }) new AtividadeRepository(db).Adicionar(a);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var consultas = new AtividadeQueries(leitura, new RelogioTeste(Agora));
        var lista = await consultas.ListarAsync(new FiltroAtividades(null, null, outroCurso, prof, null, null, curso, 1, 20), ct);
        Assert.Empty(lista.Itens);

        lista = await consultas.ListarAsync(new FiltroAtividades(null, null, null, prof, null, null, curso, 1, 20), ct);
        var item = Assert.Single(lista.Itens);
        Assert.Equal((new DateOnly(2026, 9, 1), Turno.Noite, 90), (item.Data, item.Turno, item.DuracaoMinutos));
        Assert.Equal(1, lista.Total);

        var todas = await consultas.ListarAsync(new FiltroAtividades(null, null, null, prof, null, true, null, 1, 20), ct);
        Assert.Equal(institucional.Id, Assert.Single(todas.Itens).Id);

        Assert.Null(await consultas.ObterAsync(institucional.Id, curso, ct));
        var detalhe = await consultas.ObterAsync(doCurso.Id, curso, ct);
        Assert.NotNull(detalhe);
        Assert.NotEmpty(detalhe.Versao);
    }

    private async Task<Guid> RegistrarAvulsaAsync(Guid prof, DateTimeOffset inicio, DateTimeOffset agora)
    {
        await using var db = fx.CriarContexto();
        return await Registrador(db, new UsuarioAtualTeste(prof), agora).Handle(new RegistrarAtividadeCommand(null, null, inicio,
            inicio.AddHours(1), null, TipoAtividade.RodaConversa, "Roda de conversa", "Atividade sem agendamento"),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Atividade_ocupa_o_horario_do_profissional()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var inicio = new DateTimeOffset(2026, 11, 10, 7, 0, 0, TimeSpan.FromHours(-4));
        var depois = inicio.AddDays(1);
        var id = await RegistrarAvulsaAsync(prof, inicio, depois);

        // avulsa sobre avulsa
        await Assert.ThrowsAsync<ConflictException>(() => RegistrarAvulsaAsync(prof, inicio.AddMinutes(30), depois));

        // compromisso criado sobre atividade registrada
        await using (var db = fx.CriarContexto())
        {
            var usuario = new UsuarioAtualTeste(prof);
            var relogio = SqlServerFixture.Relogio();
            var criar = new CriarCompromissoHandler(new CriarCompromissoValidator(), new AgendaRepository(db), new AlunoRepository(db),
                new UsuarioQueries(db), usuario, AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo(),
                new ServicoEscala(new EscalaRepository(db), new FeriadoRepository(db, relogio), relogio),
                new ServicoFechamento(new FechamentoRepository(db), relogio));
            await Assert.ThrowsAsync<ConflictException>(() => criar.Handle(new CriarCompromissoCommand(TipoCompromisso.ReuniaoEquipe,
                "Reunião", inicio.AddMinutes(15), inicio.AddMinutes(45), null, null, null), ct));
        }

        // corrigir só a descrição não conflita com a própria atividade
        await using var dbC = fx.CriarContexto();
        var usuarioC = new UsuarioAtualTeste(prof);
        var relogioC = new RelogioTeste(depois);
        var corrigir = new CorrigirAtividadeHandler(new CorrigirAtividadeValidator(), new AtividadeRepository(dbC),
            new AtividadeQueries(dbC, relogioC), new AgendaRepository(dbC), new CursoQueries(dbC), usuarioC, relogioC,
            AuditoriaTeste.Criar(dbC, usuarioC, relogioC), new UnidadeDeTrabalho(dbC), new CacheNulo(),
            new ServicoFechamento(new FechamentoRepository(dbC), relogioC),
            new ServicoEscala(new EscalaRepository(dbC), new FeriadoRepository(dbC, relogioC), relogioC));
        var versao = (await new AtividadeQueries(dbC, relogioC).ObterAsync(id, null, ct))!.Versao;
        var dto = await corrigir.Handle(new CorrigirAtividadeCommand(id, versao, inicio, inicio.AddHours(1), null,
            TipoAtividade.RodaConversa, "Descrição corrigida", "Ajuste de descrição"), ct);
        Assert.Equal("Descrição corrigida", dto.Descricao);
    }

    private static readonly DateTimeOffset Dia = new(2026, 11, 17, 0, 0, 0, TimeSpan.FromHours(-4));

    private async Task<Compromisso> AgendarAsync(Guid prof, TipoCompromisso tipo, DateTimeOffset inicio, DateTimeOffset fim)
    {
        var c = Compromisso.Criar(tipo, "Compromisso", IntervaloHorario.Criar(inicio, fim), prof, null, null, prof);
        await using var db = fx.CriarContexto();
        db.Compromissos.Add(c);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return c;
    }

    private async Task RegistrarDeCompromissoAsync(Guid prof, Compromisso c, DateTimeOffset fim)
    {
        await using var db = fx.CriarContexto();
        await Registrador(db, new UsuarioAtualTeste(prof), Dia.AddDays(1)).Handle(new RegistrarAtividadeCommand(c.Id, null,
            c.Intervalo.Inicio, fim, null, TipoAtividade.RodaConversa, "Roda de conversa", null), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Registro_com_intervalo_ajustado_alem_do_proprio_compromisso_passa()
    {
        var prof = await fx.CriarProfissionalAsync();
        var c = await AgendarAsync(prof, TipoCompromisso.AtividadeCurso, Dia.AddHours(9), Dia.AddHours(10));

        await RegistrarDeCompromissoAsync(prof, c, Dia.AddHours(10.5));

        await using var leitura = fx.CriarContexto();
        Assert.Equal(1, await leitura.AtividadesCurso.CountAsync(a => a.CompromissoId == c.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Registro_com_intervalo_ajustado_sobre_outro_compromisso_do_profissional_conflita()
    {
        var prof = await fx.CriarProfissionalAsync();
        var c = await AgendarAsync(prof, TipoCompromisso.AtividadeCurso, Dia.AddHours(9), Dia.AddHours(10));
        await AgendarAsync(prof, TipoCompromisso.ReuniaoEquipe, Dia.AddHours(10), Dia.AddHours(11));

        await Assert.ThrowsAsync<ConflictException>(() => RegistrarDeCompromissoAsync(prof, c, Dia.AddHours(10.5)));
    }

    [Fact]
    public async Task Avulsa_e_ajuste_de_inicio_fora_da_escala_sao_rejeitados()
    {
        var prof = await fx.CriarProfissionalAsync();
        var c = await AgendarAsync(prof, TipoCompromisso.AtividadeCurso, Dia.AddHours(9), Dia.AddHours(10));
        await using var db = fx.CriarContexto();
        var noturna = Dia.AddHours(22); // fora da escala padrão (07:00–17:00)

        var ex = await Assert.ThrowsAsync<DomainException>(() => Registrador(db, new UsuarioAtualTeste(prof), Dia.AddDays(1)).Handle(
            new RegistrarAtividadeCommand(null, null, noturna, noturna.AddHours(1), null, TipoAtividade.RodaConversa, "Roda noturna",
                "Atividade sem agendamento"), TestContext.Current.CancellationToken));
        Assert.Equal("Fora da escala do profissional neste dia.", ex.Message);
        await Assert.ThrowsAsync<DomainException>(() => Registrador(db, new UsuarioAtualTeste(prof), Dia.AddDays(1)).Handle(
            new RegistrarAtividadeCommand(c.Id, null, noturna, noturna.AddHours(1), null, TipoAtividade.RodaConversa, "Roda", null),
            TestContext.Current.CancellationToken));
    }

    private (RegistrarAtendimentoHandler Registrar, CorrigirAtendimentoHandler Corrigir) HandlersDeAtendimento(NapDbContext db, Guid prof)
    {
        var usuario = new UsuarioAtualTeste(prof);
        var relogio = new RelogioTeste(Dia.AddDays(1));
        var escala = new ServicoEscala(new EscalaRepository(db), new FeriadoRepository(db, relogio), relogio);
        var auditoria = AuditoriaTeste.Criar(db, usuario, relogio);
        return (new RegistrarAtendimentoHandler(new RegistrarAtendimentoValidator(), new AtendimentoRepository(db), new AgendaRepository(db),
                new AlunoRepository(db), new UsuarioQueries(db), usuario, relogio, auditoria, new UnidadeDeTrabalho(db), new CacheNulo(), escala,
                new ServicoFechamento(new FechamentoRepository(db), relogio)),
            new CorrigirAtendimentoHandler(new CorrigirAtendimentoValidator(), new AtendimentoRepository(db), new AgendaRepository(db),
                usuario, relogio, auditoria, new UnidadeDeTrabalho(db), new CacheNulo(), escala,
                new ServicoFechamento(new FechamentoRepository(db), relogio)));
    }

    private Task<Guid> RegistrarAtendimentoAvulsoAsync(NapDbContext db, Guid prof, Guid aluno, DateTimeOffset inicio) =>
        HandlersDeAtendimento(db, prof).Registrar.Handle(new RegistrarAtendimentoCommand(null, aluno, null, inicio, inicio.AddHours(1),
            TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, "Demanda", null, null, null, "Atendimento sem agendamento"),
            TestContext.Current.CancellationToken);

    private async Task<(Guid Prof, Guid Aluno)> ProfissionalEAlunoAsync()
    {
        var prof = await fx.CriarProfissionalAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, await fx.CriarCursoAsync(), prof);
        return (prof, aluno.Id);
    }

    [Fact]
    public async Task Atendimento_avulso_sobre_atividade_registrada_conflita()
    {
        var (prof, aluno) = await ProfissionalEAlunoAsync();
        await RegistrarAvulsaAsync(prof, Dia.AddHours(9), Dia.AddDays(1));

        await using var db = fx.CriarContexto();
        await Assert.ThrowsAsync<ConflictException>(() => RegistrarAtendimentoAvulsoAsync(db, prof, aluno, Dia.AddHours(9.5)));
    }

    [Fact]
    public async Task Correcao_de_atendimento_para_cima_de_atividade_conflita()
    {
        var ct = TestContext.Current.CancellationToken;
        var (prof, aluno) = await ProfissionalEAlunoAsync();
        await RegistrarAvulsaAsync(prof, Dia.AddHours(9), Dia.AddDays(1));
        await using var db = fx.CriarContexto();
        var atendimentoId = await RegistrarAtendimentoAvulsoAsync(db, prof, aluno, Dia.AddHours(14));

        await using var dbC = fx.CriarContexto();
        var versao = Convert.ToBase64String(await dbC.Atendimentos.Where(a => a.Id == atendimentoId)
            .Select(a => EF.Property<byte[]>(a, "Versao")).SingleAsync(ct));
        await Assert.ThrowsAsync<ConflictException>(() => HandlersDeAtendimento(dbC, prof).Corrigir.Handle(new CorrigirAtendimentoCommand(
            atendimentoId, EstadoAtendimento.Realizado, TipoAtendimento.Acompanhamento, Dia.AddHours(9.5), Dia.AddHours(10.5), "Demanda",
            null, null, null, "Ajuste de horário", versao), ct));
    }

    [Fact]
    public async Task Painel_de_atividades_respeita_a_virada_do_mes_no_fuso_institucional()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var fuso = TimeSpan.FromHours(-4);
        AtividadeCurso Nova(DateTimeOffset inicio, int minutos) => AtividadeCurso.RegistrarAvulsa(prof,
            IntervaloHorario.Criar(inicio, inicio.AddMinutes(minutos)), null, TipoAtividade.RodaConversa, "Roda de conversa",
            "Sem agenda prévia", prof, Agora);
        await using (var db = fx.CriarContexto())
        {
            var repo = new AtividadeRepository(db);
            repo.Adicionar(Nova(new DateTimeOffset(2026, 9, 30, 21, 0, 0, fuso), 120)); // mês anterior (já é 01/10 em UTC)
            repo.Adicionar(Nova(new DateTimeOffset(2026, 10, 1, 10, 0, 0, fuso), 60));
            repo.Adicionar(Nova(new DateTimeOffset(2026, 10, 1, 13, 0, 0, fuso), 30));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var painel = await new PainelQueries(leitura, new RelogioTeste(Agora)).ObterAtividadesAsync(new DateOnly(2026, 10, 2), prof, null, ct);

        Assert.Equal((2, 90), (painel.NoMes, painel.MinutosNoMes));
        Assert.Equal(2, painel.Recentes.Count);
    }
}
