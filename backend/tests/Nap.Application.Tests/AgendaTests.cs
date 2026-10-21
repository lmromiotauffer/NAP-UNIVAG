using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Agenda.Commands;
using Nap.Application.Features.Agenda.Queries;
using Nap.Application.Features.Atendimentos.Commands;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Agenda;
using Nap.Domain.Alunos;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Producao;

namespace Nap.Application.Tests;

public class AgendaTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 10, 5, 13, 0, 0, TimeSpan.Zero);
    private const string ForaDaEscala = "Fora da escala do profissional neste dia.";
    private readonly List<string> _ordem = [];
    private readonly List<string> _travas = [];
    private readonly AgendaRepositoryEmMemoria _agenda;
    private readonly AlunoRepositoryEmMemoria _alunos = new();
    private readonly UnidadeDeTrabalhoFalsa _uow;
    private readonly CacheConsultasFalso _cache;
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly EscalaRepositoryEmMemoria _escalas = new();
    private readonly FeriadoRepositoryEmMemoria _feriados = new();
    private readonly Aluno _aluno = Aluno.Criar("Maria da Silva", Matricula.Criar("2024001"), Guid.NewGuid(), 3,
        new DateOnly(2026, 8, 1), Guid.NewGuid(), null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

    public AgendaTests()
    {
        _agenda = new(_ordem) { Travas = _travas };
        _uow = new(_ordem);
        _cache = new(_ordem);
        _fechamentos = new() { Travas = _travas };
        _alunos.Adicionar(_aluno);
    }

    private CriarCompromissoHandler Criar(IUsuarioAtual usuario, params Guid[] ativos) =>
        new(new CriarCompromissoValidator(), _agenda, _alunos, new UsuarioQueriesFalso(ativos), usuario, new RelogioFixo(), _auditoria, _uow, _cache,
            Escala(), Fechamento());

    private ServicoEscala Escala() => new(_escalas, _feriados, new RelogioFixo());

    private readonly FechamentoRepositoryEmMemoria _fechamentos;

    private ServicoFechamento Fechamento() => new(_fechamentos, new RelogioFixo());

    private RemarcarCompromissoHandler Remarcar(IUsuarioAtual usuario) =>
        new(new RemarcarCompromissoValidator(), _agenda, usuario, new RelogioFixo(), _auditoria, _uow, _cache, Escala(), Fechamento());

    private static CriarCompromissoCommand Comando(TipoCompromisso tipo = TipoCompromisso.ReuniaoEquipe, Guid? profissional = null,
        Guid? aluno = null, DateTimeOffset? inicio = null) =>
        new(tipo, "Reunião", inicio ?? Inicio, (inicio ?? Inicio).AddHours(1), profissional, aluno, null);

    private static UsuarioAtualFalso Profissional() => new(Perfis.ProfissionalNap);

    [Fact]
    public async Task Conflito_detectado_gera_conflict_exception()
    {
        var h = Criar(Profissional());
        await h.Handle(Comando(), TestContext.Current.CancellationToken);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Handle(Comando(inicio: Inicio.AddMinutes(30)), TestContext.Current.CancellationToken));
        Assert.Equal("Conflito de horário com outro compromisso, atendimento ou atividade do profissional ou do aluno", ex.Message);
        Assert.Single(_agenda.Compromissos);
    }

    [Fact]
    public async Task Escrita_invalida_o_cache_do_painel_depois_de_salvar()
    {
        await Criar(Profissional()).Handle(Comando(), TestContext.Current.CancellationToken);
        Assert.Equal(["transacao", "bloquear", "conflito", "salvar", "invalidar"], _ordem);
        Assert.Equal(1, _cache.Invalidacoes);
    }

    [Fact]
    public async Task Lembrete_ignora_checagem_de_conflito()
    {
        var h = Criar(Profissional());
        await h.Handle(Comando(), TestContext.Current.CancellationToken);
        _ordem.Clear();
        await h.Handle(Comando(TipoCompromisso.Lembrete), TestContext.Current.CancellationToken);
        Assert.Equal(2, _agenda.Compromissos.Count);
        Assert.Equal(["transacao", "bloquear", "salvar", "invalidar"], _ordem);
    }

    [Fact]
    public async Task Profissional_nao_agenda_para_outro_profissional()
    {
        var outro = Guid.NewGuid();
        var h = Criar(Profissional(), outro);
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Handle(Comando(profissional: outro), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Coordenacao_agenda_para_profissional_e_registra_autor_real()
    {
        var alvo = Guid.NewGuid();
        var coord = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        var id = await Criar(coord, alvo).Handle(Comando(profissional: alvo), TestContext.Current.CancellationToken);
        var c = Assert.Single(_agenda.Compromissos);
        Assert.Equal(id, c.Id);
        Assert.Equal(alvo, c.ProfissionalId);
        Assert.Equal(coord.Id, c.CriadoPorId);
        Assert.Equal(("agenda.criacao", "Compromisso", (Guid?)id), Assert.Single(_auditoria.Registros));
    }

    [Fact]
    public async Task Coordenacao_com_profissional_inativo_gera_validacao()
    {
        var h = Criar(new UsuarioAtualFalso(Perfis.Administrador));
        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(Comando(profissional: Guid.NewGuid()), TestContext.Current.CancellationToken));
        Assert.Contains("profissionalId", ex.Erros.Keys);
    }

    [Fact]
    public async Task Aluno_inexistente_gera_validacao_no_campo()
    {
        var h = Criar(Profissional());
        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(Comando(TipoCompromisso.Atendimento, aluno: Guid.NewGuid()), TestContext.Current.CancellationToken));
        Assert.Contains("alunoId", ex.Erros.Keys);
    }

    [Fact]
    public async Task Intervalo_invalido_gera_validacao_no_campo_fim()
    {
        var h = Criar(Profissional());
        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(Comando() with { Fim = Inicio.AddHours(-1) }, TestContext.Current.CancellationToken));
        Assert.Contains("fim", ex.Erros.Keys);
    }

    [Theory]
    [InlineData(Perfis.CoordenadorCurso)]
    [InlineData(Perfis.Sip)]
    [InlineData(Perfis.Gestao)]
    public async Task Outros_perfis_nao_criam_compromisso(string perfil)
    {
        var h = Criar(new UsuarioAtualFalso(perfil, Guid.NewGuid()));
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Handle(Comando(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Bloqueio_e_checagem_ocorrem_dentro_da_transacao()
    {
        await Criar(Profissional()).Handle(Comando(), TestContext.Current.CancellationToken);
        Assert.Equal(["transacao", "bloquear", "conflito", "salvar", "invalidar"], _ordem);
    }

    [Fact]
    public async Task Remarcar_retorna_novo_id_e_marca_original()
    {
        var usuario = Profissional();
        var id = await Criar(usuario).Handle(Comando(), TestContext.Current.CancellationToken);
        var novoId = await Remarcar(usuario).Handle(new RemarcarCompromissoCommand(id, Inicio.AddDays(1), Inicio.AddDays(1).AddHours(1), "v1"), TestContext.Current.CancellationToken);

        Assert.NotEqual(id, novoId);
        var original = _agenda.Compromissos.Single(c => c.Id == id);
        Assert.Equal(EstadoCompromisso.Remarcado, original.Estado);
        Assert.Equal(novoId, original.RemarcadoParaId);
        Assert.Equal(id, _agenda.Compromissos.Single(c => c.Id == novoId).RemarcadoDeId);
        Assert.Contains("v1", _uow.VersoesExigidas);
        Assert.Contains(_auditoria.Registros, r => r.Acao == "agenda.remarcacao");
    }

    [Fact]
    public async Task Remarcar_para_horario_que_sobrepoe_o_proprio_original_nao_conflita()
    {
        var usuario = Profissional();
        var id = await Criar(usuario).Handle(Comando(), TestContext.Current.CancellationToken);
        await Remarcar(usuario).Handle(new RemarcarCompromissoCommand(id, Inicio.AddMinutes(30), Inicio.AddMinutes(90), "v1"), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Profissional_nao_remarca_nem_cancela_compromisso_de_outro()
    {
        var id = await Criar(Profissional()).Handle(Comando(), TestContext.Current.CancellationToken);
        var outro = Profissional();
        await Assert.ThrowsAsync<ForbiddenException>(() => Remarcar(outro).Handle(new RemarcarCompromissoCommand(id, Inicio.AddDays(1), Inicio.AddDays(1).AddHours(1), "v1"), TestContext.Current.CancellationToken));
        var cancelar = new CancelarCompromissoHandler(new CancelarCompromissoValidator(), _agenda, outro, new RelogioFixo(), _auditoria, _uow, _cache, Fechamento());
        await Assert.ThrowsAsync<ForbiddenException>(() => cancelar.Handle(new CancelarCompromissoCommand(id, "Motivo", "v1"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cancelar_e_atualizar_funcionam_para_o_dono_e_cancelar_audita()
    {
        var usuario = Profissional();
        var id = await Criar(usuario).Handle(Comando(), TestContext.Current.CancellationToken);
        await new AtualizarCompromissoHandler(new AtualizarCompromissoValidator(), _agenda, usuario, _auditoria, _uow, _cache)
            .Handle(new AtualizarCompromissoCommand(id, "Novo título", null, "v1"), TestContext.Current.CancellationToken);
        Assert.Equal("Novo título", _agenda.Compromissos.Single().Titulo);
        Assert.Contains("v1", _uow.VersoesExigidas);

        await new CancelarCompromissoHandler(new CancelarCompromissoValidator(), _agenda, usuario, new RelogioFixo(), _auditoria, _uow, _cache, Fechamento())
            .Handle(new CancelarCompromissoCommand(id, "Imprevisto", "v1"), TestContext.Current.CancellationToken);
        Assert.Equal(EstadoCompromisso.Cancelado, _agenda.Compromissos.Single().Estado);
        Assert.Contains(_auditoria.Registros, r => r.Acao == "agenda.cancelamento");
    }

    [Fact]
    public async Task Sobreposicao_so_pelo_aluno_com_outro_profissional_gera_conflito()
    {
        await Criar(Profissional()).Handle(Comando(TipoCompromisso.Atendimento, aluno: _aluno.Id), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ConflictException>(() => Criar(Profissional()).Handle(Comando(TipoCompromisso.Atendimento, aluno: _aluno.Id), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Intervalos_consecutivos_sao_permitidos()
    {
        var h = Criar(Profissional());
        await h.Handle(Comando(inicio: Inicio), TestContext.Current.CancellationToken);
        await h.Handle(Comando(inicio: Inicio.AddHours(1)), TestContext.Current.CancellationToken);
        Assert.Equal(2, _agenda.Compromissos.Count);
    }

    [Fact]
    public async Task Remarcar_para_horario_ocupado_gera_conflito()
    {
        var usuario = Profissional();
        var id = await Criar(usuario).Handle(Comando(), TestContext.Current.CancellationToken);
        await Criar(usuario).Handle(Comando(inicio: Inicio.AddDays(1)), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ConflictException>(() => Remarcar(usuario).Handle(new RemarcarCompromissoCommand(id, Inicio.AddDays(1), Inicio.AddDays(1).AddHours(1), "v1"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Bloqueio_sobre_compromisso_existente_retorna_409_com_os_conflitos()
    {
        var usuario = Profissional();
        var reuniao = await Criar(usuario).Handle(Comando(), TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<ConflitoAgendaException>(() =>
            Criar(usuario).Handle(Comando(TipoCompromisso.Bloqueio), TestContext.Current.CancellationToken));

        Assert.Equal(reuniao, Assert.Single(ex.Conflitos).Id);
        Assert.Single(_agenda.Compromissos);
    }

    [Fact]
    public async Task Remarcar_bloqueio_sobre_ocupado_lista_os_conflitos()
    {
        var usuario = Profissional();
        var reuniao = await Criar(usuario).Handle(Comando(), TestContext.Current.CancellationToken);
        var bloqueio = await Criar(usuario).Handle(Comando(TipoCompromisso.Bloqueio, inicio: Inicio.AddDays(1)), TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<ConflitoAgendaException>(() =>
            Remarcar(usuario).Handle(new RemarcarCompromissoCommand(bloqueio, Inicio, Inicio.AddHours(1), "v1"), TestContext.Current.CancellationToken));

        Assert.Equal(reuniao, Assert.Single(ex.Conflitos).Id);
    }

    [Fact]
    public async Task Atendimento_em_conflito_mantem_o_409_sem_lista()
    {
        var usuario = Profissional();
        await Criar(usuario).Handle(Comando(), TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAnyAsync<ConflictException>(() =>
            Criar(usuario).Handle(Comando(TipoCompromisso.Atendimento, aluno: _aluno.Id), TestContext.Current.CancellationToken));

        Assert.IsNotType<ConflitoAgendaException>(ex);
    }

    [Fact]
    public async Task Atualizar_por_profissional_nao_dono_e_negado()
    {
        var id = await Criar(Profissional()).Handle(Comando(), TestContext.Current.CancellationToken);
        var h = new AtualizarCompromissoHandler(new AtualizarCompromissoValidator(), _agenda, Profissional(), _auditoria, _uow, _cache);
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Handle(new AtualizarCompromissoCommand(id, "Outro", null, "v1"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Remarcar_cancelar_e_atualizar_por_perfil_fora_da_equipe_sao_negados()
    {
        var id = await Criar(Profissional()).Handle(Comando(), TestContext.Current.CancellationToken);
        var gestao = new UsuarioAtualFalso(Perfis.Gestao);
        await Assert.ThrowsAsync<ForbiddenException>(() => Remarcar(gestao).Handle(new RemarcarCompromissoCommand(id, Inicio.AddDays(1), Inicio.AddDays(1).AddHours(1), "v1"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ForbiddenException>(() => new CancelarCompromissoHandler(new CancelarCompromissoValidator(), _agenda, gestao, new RelogioFixo(), _auditoria, _uow, _cache, Fechamento())
            .Handle(new CancelarCompromissoCommand(id, "Motivo", "v1"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ForbiddenException>(() => new AtualizarCompromissoHandler(new AtualizarCompromissoValidator(), _agenda, gestao, _auditoria, _uow, _cache)
            .Handle(new AtualizarCompromissoCommand(id, "Outro", null, "v1"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Coordenacao_remarca_e_cancela_compromisso_de_outro_profissional()
    {
        var id = await Criar(Profissional()).Handle(Comando(), TestContext.Current.CancellationToken);
        var coord = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        var novo = await Remarcar(coord).Handle(new RemarcarCompromissoCommand(id, Inicio.AddDays(1), Inicio.AddDays(1).AddHours(1), "v1"), TestContext.Current.CancellationToken);
        await new CancelarCompromissoHandler(new CancelarCompromissoValidator(), _agenda, coord, new RelogioFixo(), _auditoria, _uow, _cache, Fechamento())
            .Handle(new CancelarCompromissoCommand(novo, "Reorganização", "v1"), TestContext.Current.CancellationToken);
        Assert.Equal(EstadoCompromisso.Cancelado, _agenda.Compromissos.Single(c => c.Id == novo).Estado);
    }

    [Fact]
    public async Task Criar_e_remarcar_em_fim_de_semana_sao_rejeitados()
    {
        var usuario = Profissional();
        var sabado = new DateTimeOffset(2026, 10, 10, 13, 0, 0, TimeSpan.Zero);
        var ex = await Assert.ThrowsAsync<DomainException>(() => Criar(usuario).Handle(Comando(inicio: sabado), TestContext.Current.CancellationToken));
        Assert.Equal(ForaDaEscala, ex.Message);
        Assert.Empty(_agenda.Compromissos);

        var id = await Criar(usuario).Handle(Comando(), TestContext.Current.CancellationToken);
        var domingo = sabado.AddDays(1);
        ex = await Assert.ThrowsAsync<DomainException>(() => Remarcar(usuario).Handle(
            new RemarcarCompromissoCommand(id, domingo, domingo.AddHours(1), "v1"), TestContext.Current.CancellationToken));
        Assert.Equal(ForaDaEscala, ex.Message);
        Assert.Equal(EstadoCompromisso.Agendado, Assert.Single(_agenda.Compromissos).Estado);
    }

    [Fact]
    public async Task Inicio_no_horario_de_fechamento_e_rejeitado()
    {
        var fechamento = new DateTimeOffset(2026, 10, 5, 17, 0, 0, TimeSpan.Zero);
        var ex = await Assert.ThrowsAsync<DomainException>(() => Criar(Profissional()).Handle(Comando(inicio: fechamento), TestContext.Current.CancellationToken));
        Assert.Equal(ForaDaEscala, ex.Message);
        await Assert.ThrowsAsync<DomainException>(() => Criar(Profissional()).Handle(Comando(inicio: fechamento.AddHours(-10.25)), TestContext.Current.CancellationToken));
        Assert.Empty(_agenda.Compromissos);
    }

    [Fact]
    public async Task Fim_apos_o_fechamento_e_aceito()
    {
        // 16:45–17:45: começa dentro da faixa e invade o fechamento (tempo extra).
        await Criar(Profissional()).Handle(Comando(inicio: new DateTimeOffset(2026, 10, 5, 16, 45, 0, TimeSpan.Zero)), TestContext.Current.CancellationToken);
        await Criar(Profissional()).Handle(Comando(inicio: new DateTimeOffset(2026, 10, 6, 7, 0, 0, TimeSpan.Zero)), TestContext.Current.CancellationToken);
        Assert.Equal(2, _agenda.Compromissos.Count);
    }

    [Fact]
    public async Task Lembrete_e_bloqueio_ignoram_escala_e_feriado()
    {
        _feriados.Adicionar(Feriado.Criar(DateOnly.FromDateTime(Inicio.UtcDateTime), "Dia do Servidor"));
        var sabado = new DateTimeOffset(2026, 10, 10, 20, 0, 0, TimeSpan.Zero);
        var usuario = Profissional();
        await Criar(usuario).Handle(Comando(TipoCompromisso.Lembrete, inicio: sabado), TestContext.Current.CancellationToken);
        var bloqueio = await Criar(usuario).Handle(Comando(TipoCompromisso.Bloqueio, inicio: Inicio), TestContext.Current.CancellationToken);
        await Remarcar(usuario).Handle(new RemarcarCompromissoCommand(bloqueio, sabado.AddHours(-2), sabado.AddHours(-1), "v1"),
            TestContext.Current.CancellationToken);
        Assert.Equal(3, _agenda.Compromissos.Count);
    }

    [Fact]
    public async Task Feriado_rejeita_atendimento_e_remarcacao()
    {
        var usuario = Profissional();
        var id = await Criar(usuario).Handle(Comando(TipoCompromisso.Atendimento, aluno: _aluno.Id), TestContext.Current.CancellationToken);
        var terca = Inicio.AddDays(1);
        _feriados.Adicionar(Feriado.Criar(DateOnly.FromDateTime(terca.UtcDateTime), "Dia do Servidor"));

        var ex = await Assert.ThrowsAsync<DomainException>(() => Remarcar(usuario).Handle(
            new RemarcarCompromissoCommand(id, terca, terca.AddHours(1), "v1"), TestContext.Current.CancellationToken));
        Assert.Equal("Esta data é feriado: Dia do Servidor.", ex.Message);
        Assert.Equal(EstadoCompromisso.Agendado, Assert.Single(_agenda.Compromissos).Estado);

        // Atendimento só se registra depois de terminado: feriado numa terça passada.
        var tercaPassada = terca.AddDays(-7);
        _feriados.Adicionar(Feriado.Criar(DateOnly.FromDateTime(tercaPassada.UtcDateTime), "Dia do Servidor"));
        var atendimentos = new AtendimentoRepositoryEmMemoria();
        var registrar = new RegistrarAtendimentoHandler(new RegistrarAtendimentoValidator(), atendimentos, _agenda, _alunos,
            new UsuarioQueriesFalso(), usuario, new RelogioFixo(), _auditoria, _uow, _cache, Escala(), Fechamento());
        ex = await Assert.ThrowsAsync<DomainException>(() => registrar.Handle(new RegistrarAtendimentoCommand(null, _aluno.Id, null,
            tercaPassada, tercaPassada.AddHours(1), TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, "Demanda", null, null, null,
            "Procurou o núcleo"), TestContext.Current.CancellationToken));
        Assert.Equal("Esta data é feriado: Dia do Servidor.", ex.Message);
        Assert.Empty(atendimentos.Atendimentos);

        // Feriado inativado volta a liberar a data.
        _feriados.Feriados.First().Inativar();
        await Remarcar(usuario).Handle(new RemarcarCompromissoCommand(id, terca, terca.AddHours(1), "v1"), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Escala_vigente_do_profissional_vale_para_quem_e_agendado()
    {
        // A coordenação agenda para um profissional cuja escala só tem o sábado: vale a escala dele, não a de quem agenda.
        var alvo = Guid.NewGuid();
        _escalas.Adicionar(EscalaProfissional.Criar(alvo, new DateOnly(2026, 1, 1), null,
            [new FaixaEscala(DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(12, 0), false)]));
        var coord = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        var ex = await Assert.ThrowsAsync<DomainException>(() => Criar(coord, alvo).Handle(Comando(profissional: alvo), TestContext.Current.CancellationToken));
        Assert.Equal(ForaDaEscala, ex.Message);
        var sabado = new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
        await Criar(coord, alvo).Handle(Comando(profissional: alvo, inicio: sabado), TestContext.Current.CancellationToken);
        Assert.Single(_agenda.Compromissos);
    }

    [Fact]
    public async Task Sexta_sem_atendimento_aceita_compromisso()
    {
        await Criar(Profissional()).Handle(Comando(inicio: Inicio.AddDays(4)), TestContext.Current.CancellationToken);
        Assert.Single(_agenda.Compromissos);
    }

    [Fact]
    public async Task Compromisso_inexistente_gera_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Remarcar(Profissional()).Handle(new RemarcarCompromissoCommand(Guid.NewGuid(), Inicio, Inicio.AddHours(1), "v1"), TestContext.Current.CancellationToken));
    }

    // Compromissos da equipe (grupo)

    private static CriarCompromissoCommand Grupo(IReadOnlyList<Guid>? participantes, TipoCompromisso tipo = TipoCompromisso.ReuniaoEquipe,
        bool todaEquipe = false) =>
        Comando(tipo) with { ParticipantesIds = participantes, TodaEquipe = todaEquipe };

    private CancelarCompromissoHandler Cancelar(IUsuarioAtual usuario) =>
        new(new CancelarCompromissoValidator(), _agenda, usuario, new RelogioFixo(), _auditoria, _uow, _cache, Fechamento());

    private static IEnumerable<Guid> OrdemDasTravas(IEnumerable<Guid> ids) => ids.OrderBy(id => id.ToString("N"), StringComparer.Ordinal);

    private Compromisso InstanciaDe(Guid profissionalId) =>
        _agenda.Compromissos.Single(c => c.ProfissionalId == profissionalId && c.Estado == EstadoCompromisso.Agendado);

    [Fact]
    public async Task Cria_uma_instancia_por_participante_com_o_mesmo_grupo()
    {
        var eu = Profissional();
        var colegas = new[] { Guid.NewGuid(), Guid.NewGuid() };
        Guid[] participantes = [eu.Id, .. colegas];

        var id = await Criar(eu, participantes).Handle(Grupo(participantes), TestContext.Current.CancellationToken);

        Assert.Equal(3, _agenda.Compromissos.Count);
        Assert.Equal(participantes.Order(), _agenda.Compromissos.Select(c => c.ProfissionalId).Order());
        var grupoId = Assert.Single(_agenda.Compromissos.Select(c => c.GrupoId).Distinct());
        Assert.NotNull(grupoId);
        Assert.All(_agenda.Compromissos, c => Assert.Equal(eu.Id, c.CriadoPorId));
        Assert.Equal(id, InstanciaDe(eu.Id).Id);
        Assert.Equal(3, _auditoria.Registros.Count(r => r.Acao == "agenda.criacao"));
        // Todas as travas de fechamento (compartilhadas), em ordem, antes da trava das agendas.
        Assert.Equal(OrdemDasTravas(participantes), _fechamentos.Bloqueios.Select(b => b.ProfissionalId));
        Assert.All(_fechamentos.Bloqueios, b => Assert.False(b.Exclusivo));
        Assert.Equal(["transacao", "bloquear", "conflito", "conflito", "conflito", "salvar", "invalidar"], _ordem);
        // Ids e ordem das travas: fechamento de todos (em ordem) e só depois a agenda de todos os participantes.
        var fechamentos = OrdemDasTravas(participantes).Select(id => $"fechamento:prof:{id:N}").ToList();
        Assert.Equal(fechamentos, _travas.Take(3));
        Assert.Equal(participantes.Select(id => $"agenda:prof:{id:N}").Order(), _travas.Skip(3).Order());
        Assert.Equal(6, _travas.Count);
    }

    [Fact]
    public async Task Toda_a_equipe_expande_para_os_ativos()
    {
        var ativos = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        await Criar(new UsuarioAtualFalso(Perfis.CoordenadorNap), ativos).Handle(Grupo(null, todaEquipe: true), TestContext.Current.CancellationToken);
        Assert.Equal(ativos.Order(), _agenda.Compromissos.Select(c => c.ProfissionalId).Order());
    }

    [Theory]
    [InlineData(TipoCompromisso.ElaboracaoPei)]
    [InlineData(TipoCompromisso.Bloqueio)]
    [InlineData(TipoCompromisso.TarefaAdministrativa)]
    public async Task Tipo_sem_grupo_com_participantes_retorna_400(TipoCompromisso tipo)
    {
        var eu = Profissional();
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Criar(eu, eu.Id).Handle(Grupo([eu.Id], tipo), TestContext.Current.CancellationToken));
        Assert.Contains("participantesIds", ex.Erros.Keys);
        Assert.Empty(_agenda.Compromissos);
    }

    [Fact]
    public async Task Grupo_com_mais_de_50_ou_com_inativo_retorna_400()
    {
        var coord = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        var muitos = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray();
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Criar(coord, muitos).Handle(Grupo(muitos), TestContext.Current.CancellationToken));
        Assert.Contains("participantesIds", ex.Erros.Keys);
        ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Criar(coord, muitos[0]).Handle(Grupo([muitos[0], Guid.NewGuid()]), TestContext.Current.CancellationToken));
        Assert.Contains("participantesIds", ex.Erros.Keys);
        Assert.Empty(_agenda.Compromissos);
    }

    [Fact]
    public async Task Conflito_de_um_participante_nao_cria_nenhuma_instancia_e_lista_quem()
    {
        var eu = Profissional();
        var livre = Guid.NewGuid();
        var ocupado = Guid.NewGuid();
        var coord = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        var reuniao = await Criar(coord, ocupado).Handle(Comando(profissional: ocupado, inicio: Inicio.AddMinutes(30)), TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<ConflitoAgendaException>(() =>
            Criar(eu, eu.Id, livre, ocupado).Handle(Grupo([eu.Id, livre, ocupado]), TestContext.Current.CancellationToken));

        var conflito = Assert.Single(ex.Conflitos);
        Assert.Equal((reuniao, ocupado), (conflito.Id, conflito.ProfissionalId));
        Assert.Single(_agenda.Compromissos);
    }

    [Fact]
    public async Task Lembrete_de_grupo_nao_checa_conflito()
    {
        var eu = Profissional();
        var colega = Guid.NewGuid();
        await Criar(new UsuarioAtualFalso(Perfis.CoordenadorNap), colega).Handle(Comando(profissional: colega), TestContext.Current.CancellationToken);
        _ordem.Clear();

        await Criar(eu, eu.Id, colega).Handle(Grupo([eu.Id, colega], TipoCompromisso.Lembrete), TestContext.Current.CancellationToken);

        Assert.Equal(3, _agenda.Compromissos.Count);
        Assert.Equal(["transacao", "bloquear", "salvar", "invalidar"], _ordem);
    }

    [Fact]
    public async Task Profissional_nao_cria_grupo_sem_si_mesmo()
    {
        var colegas = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Criar(Profissional(), colegas).Handle(Grupo(colegas), TestContext.Current.CancellationToken));
        Assert.Contains("participantesIds", ex.Erros.Keys);
        Assert.Empty(_agenda.Compromissos);
    }

    [Fact]
    public async Task Coordenacao_cria_grupo_sem_participar()
    {
        var coord = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        var participantes = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await Criar(coord, participantes).Handle(Grupo(participantes), TestContext.Current.CancellationToken);
        Assert.Equal(participantes.Order(), _agenda.Compromissos.Select(c => c.ProfissionalId).Order());
        Assert.All(_agenda.Compromissos, c => Assert.Equal(coord.Id, c.CriadoPorId));
    }

    [Fact]
    public async Task Remarcar_grupo_move_todas_as_agendadas()
    {
        var eu = Profissional();
        var colega = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var desistente = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        Guid[] participantes = [eu.Id, colega.Id, desistente.Id];
        await Criar(eu, participantes).Handle(Grupo(participantes), TestContext.Current.CancellationToken);
        var grupoId = _agenda.Compromissos[0].GrupoId;
        await Cancelar(desistente).Handle(new CancelarCompromissoCommand(InstanciaDe(desistente.Id).Id, "Não poderei", "v1"),
            TestContext.Current.CancellationToken);
        var minha = InstanciaDe(eu.Id);
        _fechamentos.Bloqueios.Clear();

        var amanha = Inicio.AddDays(1);
        var novoId = await Remarcar(eu).Handle(new RemarcarCompromissoCommand(minha.Id, amanha, amanha.AddHours(1), "v1", EscopoGrupo.Grupo),
            TestContext.Current.CancellationToken);

        var agendados = _agenda.Compromissos.Where(c => c.Estado == EstadoCompromisso.Agendado).ToList();
        Assert.Equal(new[] { eu.Id, colega.Id }.Order(), agendados.Select(c => c.ProfissionalId).Order());
        Assert.All(agendados, c => Assert.Equal((amanha, grupoId), (c.Intervalo.Inicio, c.GrupoId)));
        Assert.Equal(novoId, InstanciaDe(eu.Id).Id);
        Assert.Equal(EstadoCompromisso.Cancelado, _agenda.Compromissos.Single(c => c.ProfissionalId == desistente.Id).Estado);
        Assert.Equal(2, _agenda.Compromissos.Count(c => c.Estado == EstadoCompromisso.Remarcado));
        Assert.Equal(2, _auditoria.Registros.Count(r => r.Acao == "agenda.remarcacao"));
        // Fechamento de todos (semana antiga e nova) antes das agendas, em ordem.
        Assert.Equal(OrdemDasTravas([eu.Id, colega.Id]).SelectMany(id => new[] { id, id }), _fechamentos.Bloqueios.Select(b => b.ProfissionalId));
    }

    [Fact]
    public async Task Participante_nao_remarca_o_grupo()
    {
        var eu = Profissional();
        var colega = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        await Criar(eu, eu.Id, colega.Id).Handle(Grupo([eu.Id, colega.Id]), TestContext.Current.CancellationToken);
        var daColega = InstanciaDe(colega.Id).Id;
        var amanha = Inicio.AddDays(1);

        // Em instância de grupo, "minha" só cancela: remarcar/atualizar sem o escopo do grupo é 400 (nada muda).
        var remarcarMinha = await Assert.ThrowsAsync<ValidationException>(() => Remarcar(colega).Handle(
            new RemarcarCompromissoCommand(daColega, amanha, amanha.AddHours(1), "v1"), TestContext.Current.CancellationToken));
        Assert.Equal([GrupoCompromisso.MensagemMinhaEmGrupo], remarcarMinha.Erros["escopo"]);
        var atualizarMinha = await Assert.ThrowsAsync<ValidationException>(() => new AtualizarCompromissoHandler(new AtualizarCompromissoValidator(), _agenda, colega, _auditoria, _uow, _cache)
            .Handle(new AtualizarCompromissoCommand(daColega, "Outro", null, "v1"), TestContext.Current.CancellationToken));
        Assert.Contains("escopo", atualizarMinha.Erros.Keys);
        await Assert.ThrowsAsync<ForbiddenException>(() => Cancelar(colega).Handle(
            new CancelarCompromissoCommand(daColega, "Motivo", "v1", EscopoGrupo.Grupo), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ForbiddenException>(() => new AtualizarCompromissoHandler(new AtualizarCompromissoValidator(), _agenda, colega, _auditoria, _uow, _cache)
            .Handle(new AtualizarCompromissoCommand(daColega, "Outro", null, "v1", EscopoGrupo.Grupo), TestContext.Current.CancellationToken));
        Assert.Equal((Inicio, "Reunião"), (InstanciaDe(eu.Id).Intervalo.Inicio, InstanciaDe(colega.Id).Titulo));
        Assert.Equal(Inicio, InstanciaDe(colega.Id).Intervalo.Inicio);
    }

    [Fact]
    public async Task Coordenacao_com_escopo_minha_em_instancia_de_grupo_tambem_recebe_400_mas_cancelar_minha_funciona()
    {
        var coord = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        var alvo = Guid.NewGuid();
        await Criar(coord, alvo).Handle(Grupo([alvo]), TestContext.Current.CancellationToken);
        var id = InstanciaDe(alvo).Id;
        var amanha = Inicio.AddDays(1);

        await Assert.ThrowsAsync<ValidationException>(() => Remarcar(coord).Handle(
            new RemarcarCompromissoCommand(id, amanha, amanha.AddHours(1), "v1"), TestContext.Current.CancellationToken));
        await Cancelar(coord).Handle(new CancelarCompromissoCommand(id, "Motivo", "v1"), TestContext.Current.CancellationToken);

        Assert.Equal(EstadoCompromisso.Cancelado, _agenda.Compromissos.Single().Estado);
    }

    [Fact]
    public async Task Cancelar_minha_mantem_as_outras()
    {
        var eu = Profissional();
        var colega = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        await Criar(eu, eu.Id, colega.Id).Handle(Grupo([eu.Id, colega.Id]), TestContext.Current.CancellationToken);
        _ordem.Clear();

        await Cancelar(colega).Handle(new CancelarCompromissoCommand(InstanciaDe(colega.Id).Id, "Não poderei", "v1"), TestContext.Current.CancellationToken);

        Assert.Equal(EstadoCompromisso.Cancelado, _agenda.Compromissos.Single(c => c.ProfissionalId == colega.Id).Estado);
        Assert.Equal(EstadoCompromisso.Agendado, _agenda.Compromissos.Single(c => c.ProfissionalId == eu.Id).Estado);
        Assert.Equal(["transacao", "bloquear", "salvar", "invalidar"], _ordem);
    }

    [Fact]
    public async Task Atualizar_e_cancelar_grupo_aplicam_a_todas_as_agendadas()
    {
        var coord = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        var participantes = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await Criar(coord, participantes).Handle(Grupo(participantes), TestContext.Current.CancellationToken);
        var alvo = InstanciaDe(participantes[0]).Id;

        await new AtualizarCompromissoHandler(new AtualizarCompromissoValidator(), _agenda, coord, _auditoria, _uow, _cache)
            .Handle(new AtualizarCompromissoCommand(alvo, "Pauta nova", null, "v1", EscopoGrupo.Grupo), TestContext.Current.CancellationToken);
        Assert.All(_agenda.Compromissos, c => Assert.Equal("Pauta nova", c.Titulo));

        await Cancelar(coord).Handle(new CancelarCompromissoCommand(alvo, "Reunião desmarcada", "v1", EscopoGrupo.Grupo), TestContext.Current.CancellationToken);
        Assert.All(_agenda.Compromissos, c => Assert.Equal(EstadoCompromisso.Cancelado, c.Estado));
        Assert.Equal(2, _auditoria.Registros.Count(r => r.Acao == "agenda.cancelamento"));
    }

    [Fact]
    public async Task Escopo_grupo_em_compromisso_individual_retorna_400()
    {
        var eu = Profissional();
        var id = await Criar(eu).Handle(Comando(), TestContext.Current.CancellationToken);
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Cancelar(eu).Handle(new CancelarCompromissoCommand(id, "Motivo", "v1", EscopoGrupo.Grupo), TestContext.Current.CancellationToken));
        Assert.Contains("escopo", ex.Erros.Keys);
    }

    [Fact]
    public async Task Semana_fechada_de_um_participante_bloqueia_o_grupo()
    {
        var eu = Profissional();
        var colega = Guid.NewGuid();
        var fechada = FechamentoSemanal.Criar(colega, new DateOnly(2026, 10, 5));
        fechada.Fechar(Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2026, 10, 20));
        _fechamentos.Adicionar(fechada);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Criar(eu, eu.Id, colega).Handle(Grupo([eu.Id, colega]), TestContext.Current.CancellationToken));

        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);
        Assert.Empty(_agenda.Compromissos);
    }

    [Fact]
    public async Task Semana_deve_iniciar_na_segunda()
    {
        var h = new ListarAgendaSemanaHandler(new ListarAgendaSemanaValidator(), new AgendaQueriesFalso(), Profissional());
        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(new ListarAgendaSemanaQuery(new DateOnly(2026, 10, 6)), TestContext.Current.CancellationToken));
        Assert.Contains("inicio", ex.Erros.Keys);

        var semana = await h.Handle(new ListarAgendaSemanaQuery(new DateOnly(2026, 10, 5)), TestContext.Current.CancellationToken);
        Assert.Equal(new DateOnly(2026, 10, 5), semana.Inicio);
    }

    [Fact]
    public async Task Consulta_de_agenda_negada_a_outros_perfis()
    {
        var gestao = new UsuarioAtualFalso(Perfis.Gestao);
        var h = new ListarAgendaSemanaHandler(new ListarAgendaSemanaValidator(), new AgendaQueriesFalso(), gestao);
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Handle(new ListarAgendaSemanaQuery(new DateOnly(2026, 10, 5)), TestContext.Current.CancellationToken));
    }
}
