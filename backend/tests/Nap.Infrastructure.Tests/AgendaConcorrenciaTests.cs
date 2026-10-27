using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Agenda.Commands;
using Nap.Application.Features.Atendimentos.Commands;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Consultas;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class AgendaConcorrenciaTests(SqlServerFixture fx)
{
    private static readonly DateTimeOffset Base = new(2026, 11, 9, 7, 0, 0, TimeSpan.FromHours(-4));

    private static ServicoEscala Escala(NapDbContext db, IRelogio relogio) =>
        new(new EscalaRepository(db), new FeriadoRepository(db, relogio), relogio);

    private async Task<Guid> CriarAsync(Guid profissionalId, Guid? alunoId, DateTimeOffset inicio, DateTimeOffset fim)
    {
        await using var db = fx.CriarContexto();
        var usuario = new UsuarioAtualTeste(profissionalId);
        var relogio = SqlServerFixture.Relogio();
        var handler = new CriarCompromissoHandler(new CriarCompromissoValidator(), new AgendaRepository(db), new AlunoRepository(db),
            new UsuarioQueries(db), usuario, AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo(),
            Escala(db, relogio), new ServicoFechamento(new FechamentoRepository(db), relogio));
        return await handler.Handle(new CriarCompromissoCommand(alunoId is null ? TipoCompromisso.ReuniaoEquipe : TipoCompromisso.Atendimento,
            "Compromisso", inicio, fim, null, alunoId, null), TestContext.Current.CancellationToken);
    }

    private static async Task<(int Sucessos, int Conflitos)> Contar(IEnumerable<Task> tarefas)
    {
        var lista = tarefas.ToList();
        try { await Task.WhenAll(lista); } catch { /* avaliado abaixo */ }
        Assert.All(lista.Where(t => t.IsFaulted), t => Assert.IsType<ConflictException>(t.Exception!.InnerException));
        return (lista.Count(t => t.IsCompletedSuccessfully), lista.Count(t => t.IsFaulted));
    }

    [Fact]
    public async Task Reservas_simultaneas_do_mesmo_profissional_geram_um_unico_compromisso()
    {
        var prof = await fx.CriarProfissionalAsync();
        var (sucessos, conflitos) = await Contar(Enumerable.Range(0, 20).Select(_ => Task.Run(() => CriarAsync(prof, null, Base, Base.AddHours(1)))));

        Assert.Equal((1, 19), (sucessos, conflitos));
        await using var db = fx.CriarContexto();
        Assert.Equal(1, await db.Compromissos.CountAsync(c => c.ProfissionalId == prof, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reservas_simultaneas_do_mesmo_aluno_com_profissionais_diferentes_geram_um_unico()
    {
        var profs = new List<Guid>();
        for (var i = 0; i < 20; i++) profs.Add(await fx.CriarProfissionalAsync());
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, await fx.CriarCursoAsync(), profs[0]);

        var (sucessos, conflitos) = await Contar(profs.Select(p => Task.Run(() => CriarAsync(p, aluno.Id, Base, Base.AddHours(1)))));

        Assert.Equal((1, 19), (sucessos, conflitos));
        await using var db = fx.CriarContexto();
        Assert.Equal(1, await db.Compromissos.CountAsync(c => c.AlunoId == aluno.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Grupos_simultaneos_com_participantes_em_comum_nao_reservam_duas_vezes()
    {
        var ct = TestContext.Current.CancellationToken;
        var coord = await fx.CriarProfissionalAsync(Perfis.CoordenadorNap);
        var profs = new List<Guid>();
        for (var i = 0; i < 6; i++) profs.Add(await fx.CriarProfissionalAsync());

        // Cada grupo tem 3 participantes consecutivos (em círculo): grupos vizinhos se sobrepõem em ordens de trava diferentes.
        var tarefas = Enumerable.Range(0, 12).Select(i => Task.Run(async () =>
        {
            await using var db = fx.CriarContexto();
            var usuario = new UsuarioAtualTeste(coord, Perfis.CoordenadorNap);
            var relogio = SqlServerFixture.Relogio();
            var handler = new CriarCompromissoHandler(new CriarCompromissoValidator(), new AgendaRepository(db), new AlunoRepository(db),
                new UsuarioQueries(db), usuario, AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo(),
                Escala(db, relogio), new ServicoFechamento(new FechamentoRepository(db), relogio));
            Guid[] participantes = [profs[i % 6], profs[(i + 1) % 6], profs[(i + 2) % 6]];
            return await handler.Handle(new CriarCompromissoCommand(TipoCompromisso.ReuniaoEquipe, "Reunião", Base, Base.AddHours(1),
                null, null, null, i % 2 == 0 ? participantes : [.. participantes.Reverse()]), ct);
        }, ct)).ToList();
        try { await Task.WhenAll(tarefas); } catch { /* avaliado abaixo */ }

        // Sem deadlock nem espera esgotada: toda falha é o 409 de conflito com a lista de quem está ocupado.
        Assert.All(tarefas.Where(t => t.IsFaulted), t => Assert.NotEmpty(Assert.IsType<ConflitoAgendaException>(t.Exception!.InnerException).Conflitos));
        var sucessos = tarefas.Count(t => t.IsCompletedSuccessfully);
        Assert.InRange(sucessos, 1, 2);
        await using var leitura = fx.CriarContexto();
        var reservas = await leitura.Compromissos.Where(c => profs.Contains(c.ProfissionalId)).ToListAsync(ct);
        Assert.Equal(sucessos * 3, reservas.Count);
        Assert.All(reservas.GroupBy(c => c.ProfissionalId), g => Assert.Single(g));
    }

    /// <summary>
    /// Cancelamento individual de uma irmã contra edição do grupo, as duas já com a leitura feita: a trava da agenda da irmã é segurada
    /// até as duas estarem esperando por ela. Uma vence; a outra recebe 409 (rowversion) e nada é gravado pela perdedora.
    /// </summary>
    private async Task CorridaIrmaContraGrupoAsync(bool grupoRemarca)
    {
        var ct = TestContext.Current.CancellationToken;
        var coord = await fx.CriarProfissionalAsync(Perfis.CoordenadorNap);
        Guid[] profs = [await fx.CriarProfissionalAsync(), await fx.CriarProfissionalAsync(), await fx.CriarProfissionalAsync()];
        var inicio = Base.AddDays(grupoRemarca ? 3 : 4);
        await using (var db = fx.CriarContexto())
        {
            var usuario = new UsuarioAtualTeste(coord, Perfis.CoordenadorNap);
            var relogio = SqlServerFixture.Relogio();
            await new CriarCompromissoHandler(new CriarCompromissoValidator(), new AgendaRepository(db), new AlunoRepository(db),
                new UsuarioQueries(db), usuario, AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo(),
                Escala(db, relogio), new ServicoFechamento(new FechamentoRepository(db), relogio))
                .Handle(new CriarCompromissoCommand(TipoCompromisso.ReuniaoEquipe, "Reunião", inicio, inicio.AddHours(1), null, null, null, profs), ct);
        }
        await using var leitura = fx.CriarContexto();
        var originais = await leitura.Compromissos.AsNoTracking().Where(c => profs.Contains(c.ProfissionalId)).ToListAsync(ct);
        var irma = originais.First(c => c.ProfissionalId == profs[2]);
        var outra = originais.First(c => c.ProfissionalId == profs[0]);
        async Task<string> VersaoAsync(Guid id) => Convert.ToBase64String(await leitura.Compromissos.Where(c => c.Id == id)
            .Select(c => EF.Property<byte[]>(c, "Versao")).SingleAsync(ct));
        var versaoIrma = await VersaoAsync(irma.Id);
        var versaoOutra = await VersaoAsync(outra.Id);

        // Segura a trava da agenda da irmã numa transação própria.
        var chave = $"agenda:prof:{profs[2]:N}";
        await using var travaDb = fx.CriarContexto();
        await using var trava = await travaDb.Database.BeginTransactionAsync(ct);
        await travaDb.Database.ExecuteSqlRawAsync("EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction'", [chave], ct);

        Task Individual() => Task.Run(async () =>
        {
            await using var db = fx.CriarContexto();
            var usuario = new UsuarioAtualTeste(profs[2]);
            var relogio = SqlServerFixture.Relogio();
            await new CancelarCompromissoHandler(new CancelarCompromissoValidator(), new AgendaRepository(db), usuario, relogio,
                AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo(),
                new ServicoFechamento(new FechamentoRepository(db), relogio))
                .Handle(new CancelarCompromissoCommand(irma.Id, "Não poderei ir", versaoIrma), ct);
        }, ct);
        Task Grupo() => Task.Run(async () =>
        {
            await using var db = fx.CriarContexto();
            var usuario = new UsuarioAtualTeste(coord, Perfis.CoordenadorNap);
            var relogio = SqlServerFixture.Relogio();
            var fechamento = new ServicoFechamento(new FechamentoRepository(db), relogio);
            if (grupoRemarca)
                await new RemarcarCompromissoHandler(new RemarcarCompromissoValidator(), new AgendaRepository(db), usuario,
                    AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo(), Escala(db, relogio), fechamento)
                    .Handle(new RemarcarCompromissoCommand(outra.Id, inicio.AddHours(3), inicio.AddHours(4), versaoOutra, EscopoGrupo.Grupo), ct);
            else
                await new CancelarCompromissoHandler(new CancelarCompromissoValidator(), new AgendaRepository(db), usuario, relogio,
                    AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo(), fechamento)
                    .Handle(new CancelarCompromissoCommand(outra.Id, "Reunião desmarcada", versaoOutra, EscopoGrupo.Grupo), ct);
        }, ct);
        var tarefas = new[] { Individual(), Grupo() };

        // Espera as duas pararem na trava da irmã (ambas já leram o estado antigo); sem isso a ordem seria sorte.
        for (var i = 0; i < 150; i++)
        {
            await using var consulta = fx.CriarContexto();
            var esperando = await consulta.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sys.dm_tran_locks WHERE resource_type = 'APPLICATION' AND request_status = 'WAIT' AND resource_description LIKE {0}",
                $"%{chave[..32]}%").SingleAsync(ct); // o SQL Server trunca o nome do recurso em 32 caracteres
            if (esperando >= 2) break;
            await Task.Delay(100, ct);
        }
        await trava.CommitAsync(ct);
        var (sucessos, conflitos) = await Contar(tarefas);

        Assert.Equal((1, 1), (sucessos, conflitos));
        await using var final = fx.CriarContexto();
        var estados = await final.Compromissos.AsNoTracking().Where(c => profs.Contains(c.ProfissionalId)).ToListAsync(ct);
        if (tarefas[0].IsCompletedSuccessfully)
        {
            // A irmã cancelou primeiro: o grupo foi desfeito inteiro (nada parcial), as outras continuam como estavam.
            Assert.Equal(EstadoCompromisso.Cancelado, estados.Single(c => c.ProfissionalId == profs[2]).Estado);
            Assert.All(estados.Where(c => c.ProfissionalId != profs[2]), c => Assert.Equal((EstadoCompromisso.Agendado, inicio), (c.Estado, c.Intervalo.Inicio)));
            Assert.Equal(3, estados.Count);
        }
        else if (grupoRemarca)
        {
            Assert.Equal(3, estados.Count(c => c.Estado == EstadoCompromisso.Remarcado));
            Assert.Equal(3, estados.Count(c => c.Estado == EstadoCompromisso.Agendado && c.Intervalo.Inicio == inicio.AddHours(3)));
            Assert.DoesNotContain(estados, c => c.Estado == EstadoCompromisso.Cancelado);
        }
        else
            Assert.All(estados, c => Assert.Equal(EstadoCompromisso.Cancelado, c.Estado));
    }

    [Fact]
    public Task Cancelar_irma_e_remarcar_grupo_ao_mesmo_tempo_um_vence_e_o_outro_recebe_409() => CorridaIrmaContraGrupoAsync(grupoRemarca: true);

    [Fact]
    public Task Cancelar_irma_e_cancelar_grupo_ao_mesmo_tempo_um_vence_e_o_outro_recebe_409() => CorridaIrmaContraGrupoAsync(grupoRemarca: false);

    [Fact]
    public async Task Horarios_consecutivos_sao_permitidos()
    {
        var prof = await fx.CriarProfissionalAsync();
        await CriarAsync(prof, null, Base, Base.AddHours(1));
        await CriarAsync(prof, null, Base.AddHours(1), Base.AddHours(2));

        await using var db = fx.CriarContexto();
        Assert.Equal(2, await db.Compromissos.CountAsync(c => c.ProfissionalId == prof, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Remarcacoes_simultaneas_do_mesmo_compromisso_geram_uma_unica()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var original = await CriarAsync(prof, null, Base, Base.AddHours(1));

        // Os dois contextos carregam o original antes de qualquer gravação: só o rowversion pode barrar o segundo.
        var contextos = new[] { fx.CriarContexto(), fx.CriarContexto() };
        foreach (var db in contextos) await db.Compromissos.SingleAsync(c => c.Id == original, ct);
        var versao = Convert.ToBase64String(await contextos[0].Compromissos.Where(c => c.Id == original)
            .Select(c => EF.Property<byte[]>(c, "Versao")).SingleAsync(ct));

        var tarefas = contextos.Select((db, i) => Task.Run(async () =>
        {
            var usuario = new UsuarioAtualTeste(prof);
            var relogio = SqlServerFixture.Relogio();
            var handler = new RemarcarCompromissoHandler(new RemarcarCompromissoValidator(), new AgendaRepository(db), usuario,
                AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo(), Escala(db, relogio),
                new ServicoFechamento(new FechamentoRepository(db), relogio));
            var inicio = Base.AddHours(2 + i * 2);
            return await handler.Handle(new RemarcarCompromissoCommand(original, inicio, inicio.AddHours(1), versao), ct);
        }, ct)).ToList();
        var (sucessos, conflitos) = await Contar(tarefas);
        foreach (var db in contextos) await db.DisposeAsync();

        Assert.Equal((1, 1), (sucessos, conflitos));
        // Horários-alvo distintos: quem barra o segundo é o rowversion do original, não a checagem de conflito.
        Assert.Equal("O registro foi alterado por outra pessoa. Recarregue e tente novamente",
            tarefas.Single(t => t.IsFaulted).Exception!.InnerException!.Message);
        await using var leitura = fx.CriarContexto();
        Assert.Equal(1, await leitura.Compromissos.CountAsync(c => c.RemarcadoDeId == original, ct));
    }

    [Fact]
    public async Task Conflito_considera_atendimentos_e_ignora_o_proprio_registro()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var outroProf = await fx.CriarProfissionalAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, await fx.CriarCursoAsync(), prof);
        var agora = Base.AddDays(1);
        var h1 = IntervaloHorario.Criar(Base, Base.AddHours(1));
        var h2 = IntervaloHorario.Criar(Base.AddHours(2), Base.AddHours(3));

        // h1: compromisso concluído por atendimento (quem ocupa é o atendimento); h2: atendimento avulso; h3: lembrete.
        var compromisso = Compromisso.Criar(TipoCompromisso.Atendimento, "Atendimento", h1, prof, aluno.Id, null, prof);
        var registro = Atendimento.RegistrarDeCompromisso(compromisso, TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado,
            new DadosClinicos("Demanda", null, null, null), prof, agora);
        var avulso = Atendimento.RegistrarAvulso(aluno.Id, prof, h2, TipoAtendimento.Acompanhamento, EstadoAtendimento.Ausente, null,
            "Justificativa avulso", prof, agora);
        var lembrete = Compromisso.Criar(TipoCompromisso.Lembrete, "Lembrete", IntervaloHorario.Criar(Base.AddHours(4), Base.AddHours(5)),
            prof, null, null, prof);
        await using (var db = fx.CriarContexto())
        {
            db.AddRange(compromisso, registro, avulso, lembrete);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var agenda = new AgendaRepository(leitura);
        Assert.True(await agenda.ExisteConflitoAsync(prof, null, h1, null, ct));
        Assert.True(await agenda.ExisteConflitoAsync(outroProf, aluno.Id, h1, null, ct));
        Assert.False(await agenda.ExisteConflitoAsync(prof, aluno.Id, h1, registro.Id, ct));
        // Ignorar o compromisso concluído não libera o horário: o atendimento vinculado continua ocupando.
        Assert.True(await agenda.ExisteConflitoAsync(prof, aluno.Id, h1, compromisso.Id, ct));
        Assert.True(await agenda.ExisteConflitoAsync(prof, null, h2, null, ct));
        Assert.True(await agenda.ExisteConflitoAsync(outroProf, aluno.Id, h2, null, ct));
        Assert.False(await agenda.ExisteConflitoAsync(prof, aluno.Id, h2, avulso.Id, ct));
        Assert.False(await agenda.ExisteConflitoAsync(outroProf, null, h2, null, ct));
        Assert.False(await agenda.ExisteConflitoAsync(prof, aluno.Id, IntervaloHorario.Criar(Base.AddHours(4), Base.AddHours(5)), null, ct));
        Assert.False(await agenda.ExisteConflitoAsync(prof, aluno.Id, IntervaloHorario.Criar(Base.AddHours(1), Base.AddHours(2)), null, ct));
    }

    // Segunda-feira passada no fuso institucional: Corrigir exige intervalo já encerrado (relógio real).
    private static readonly DateTimeOffset Passado = new(2026, 9, 14, 0, 0, 0, TimeSpan.FromHours(-4));

    private async Task<(Guid Prof, Guid Aluno, Guid Compromisso, Guid Atendimento)> AtendimentoDaAgendaAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, await fx.CriarCursoAsync(), prof);
        var compromissoId = await CriarAsync(prof, aluno.Id, Passado.AddHours(10), Passado.AddHours(11));
        await using var db = fx.CriarContexto();
        var compromisso = await db.Compromissos.SingleAsync(c => c.Id == compromissoId, ct);
        var atendimento = Atendimento.RegistrarDeCompromisso(compromisso, TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado,
            new DadosClinicos("Demanda", null, null, null), prof, Passado.AddHours(12));
        db.Atendimentos.Add(atendimento);
        await db.SaveChangesAsync(ct);
        return (prof, aluno.Id, compromissoId, atendimento.Id);
    }

    private async Task CorrigirAsync(Guid prof, Guid atendimentoId, EstadoAtendimento estado, int horaInicio)
    {
        await using var db = fx.CriarContexto();
        var versao = Convert.ToBase64String(await db.Atendimentos.Where(a => a.Id == atendimentoId)
            .Select(a => EF.Property<byte[]>(a, "Versao")).SingleAsync(TestContext.Current.CancellationToken));
        var usuario = new UsuarioAtualTeste(prof);
        var relogio = SqlServerFixture.Relogio();
        var handler = new CorrigirAtendimentoHandler(new CorrigirAtendimentoValidator(), new AtendimentoRepository(db),
            new AgendaRepository(db), usuario, relogio, AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo(),
            Escala(db, relogio), new ServicoFechamento(new FechamentoRepository(db), relogio));
        await handler.Handle(new CorrigirAtendimentoCommand(atendimentoId, estado, TipoAtendimento.Acompanhamento,
            Passado.AddHours(horaInicio), Passado.AddHours(horaInicio + 1), "Demanda", null, null, null, null, versao),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Correcao_de_horario_do_atendimento_move_a_ocupacao_e_a_agenda()
    {
        var ct = TestContext.Current.CancellationToken;
        var (prof, aluno, compromissoId, atendimentoId) = await AtendimentoDaAgendaAsync();

        await CorrigirAsync(prof, atendimentoId, EstadoAtendimento.Realizado, 14);

        // 14–15 passou a estar ocupado (pelo profissional e pelo aluno); 10–11 foi liberado.
        await Assert.ThrowsAsync<ConflictException>(() => CriarAsync(prof, null, Passado.AddHours(14), Passado.AddHours(15)));
        var outroProf = await fx.CriarProfissionalAsync();
        await Assert.ThrowsAsync<ConflictException>(() => CriarAsync(outroProf, aluno, Passado.AddHours(14), Passado.AddHours(15)));
        var novoId = await CriarAsync(prof, null, Passado.AddHours(10), Passado.AddHours(11));

        await using var db = fx.CriarContexto();
        var semana = await new AgendaQueries(db, SqlServerFixture.Relogio()).ListarSemanaAsync(DateOnly.FromDateTime(Passado.DateTime), prof, ct);
        Assert.Equal([novoId, compromissoId], semana.Compromissos.Select(c => c.Id));
        var concluido = semana.Compromissos[1];
        Assert.Equal((Passado.AddHours(14), Passado.AddHours(15)), (concluido.Inicio, concluido.Fim));
        Assert.Equal((EstadoCompromisso.Concluido, EstadoAtendimento.Realizado), (concluido.Estado, concluido.EstadoAtendimento));
    }

    [Fact]
    public async Task Correcao_para_cancelado_libera_o_horario()
    {
        var ct = TestContext.Current.CancellationToken;
        var (prof, _, compromissoId, atendimentoId) = await AtendimentoDaAgendaAsync();
        await Assert.ThrowsAsync<ConflictException>(() => CriarAsync(prof, null, Passado.AddHours(10), Passado.AddHours(11)));

        await CorrigirAsync(prof, atendimentoId, EstadoAtendimento.Cancelado, 10);

        await CriarAsync(prof, null, Passado.AddHours(10), Passado.AddHours(11));
        await using var db = fx.CriarContexto();
        var semana = await new AgendaQueries(db, SqlServerFixture.Relogio()).ListarSemanaAsync(DateOnly.FromDateTime(Passado.DateTime), prof, ct);
        Assert.Equal(EstadoAtendimento.Cancelado, semana.Compromissos.Single(c => c.Id == compromissoId).EstadoAtendimento);
    }

    [Fact]
    public async Task Bloqueio_fora_de_transacao_e_rejeitado()
    {
        await using var db = fx.CriarContexto();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AgendaRepository(db).BloquearAgendasAsync(Guid.NewGuid(), null, TestContext.Current.CancellationToken));
    }
}
