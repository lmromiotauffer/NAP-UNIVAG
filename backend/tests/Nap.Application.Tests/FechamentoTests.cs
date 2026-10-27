using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Agenda.Commands;
using Nap.Application.Features.Atendimentos.Commands;
using Nap.Application.Features.Atividades.Commands;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Producao.Commands;
using Nap.Application.Features.Producao.Queries;
using Nap.Application.Features.Usuarios;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Agenda;
using Nap.Domain.Alunos;
using Nap.Domain.Atendimentos;
using Nap.Domain.Atividades;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Producao;

namespace Nap.Application.Tests;

public class FechamentoTests
{
    // Relógio fixo: 2026-10-02 15:00Z (sexta). A semana de 28/09 ainda não terminou; a de 21/09 sim.
    private static readonly DateOnly SemanaEncerrada = new(2026, 9, 21);
    private static readonly DateTimeOffset NaSemanaFechada = new(2026, 9, 22, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NaSemanaAberta = new(2026, 9, 15, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly DepoisDeTudo = new(2026, 12, 1);

    private readonly FechamentoRepositoryEmMemoria _fechamentos = new();
    private readonly ProducaoQueriesFalso _consultas = new();
    private readonly AgendaRepositoryEmMemoria _agenda;
    private readonly AtendimentoRepositoryEmMemoria _atendimentos = new();
    private readonly AtividadeRepositoryEmMemoria _atividades = new();
    private readonly AlunoRepositoryEmMemoria _alunos = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly CacheConsultasFalso _cache = new();
    private readonly Aluno _aluno = Aluno.Criar("Maria da Silva", Matricula.Criar("2024001"), Guid.NewGuid(), 3,
        new DateOnly(2026, 8, 1), Guid.NewGuid(), null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

    public FechamentoTests()
    {
        _agenda = new(null, _atendimentos.Atendimentos);
        _alunos.Adicionar(_aluno);
    }

    private static UsuarioAtualFalso Profissional() => new(Perfis.ProfissionalNap);
    private static UsuarioAtualFalso Coordenacao() => new(Perfis.CoordenadorNap);

    private ServicoFechamento Servico() => new(_fechamentos, new RelogioFixo());

    private FecharSemanaHandler Fechar(IUsuarioAtual usuario, params Guid[] ativos) =>
        Fechar(usuario, ativos.Select(id => (id, Perfis.ProfissionalNap, true)).ToArray());

    private FecharSemanaHandler Fechar(IUsuarioAtual usuario, (Guid Id, string Perfil, bool Ativo)[] alvos)
    {
        // Como a consulta real sem fatos na semana: só a equipe NAP entra, ativa ou não.
        var usuarios = new UsuarioQueriesFalso(alvos.Where(a => a.Ativo && Perfis.EquipeNap.Contains(a.Perfil)).Select(a => a.Id).ToArray());
        usuarios.Usuarios.AddRange(alvos.Select(a => (a.Id, a.Perfil, (Guid?)null, a.Ativo)));
        usuarios.Inativos.AddRange(alvos.Where(a => !a.Ativo && Perfis.EquipeNap.Contains(a.Perfil))
            .Select(a => new ProfissionalProducaoDto(a.Id, "Usuário", false, null)));
        return Fechar(usuario, usuarios);
    }

    private FecharSemanaHandler Fechar(IUsuarioAtual usuario, UsuarioQueriesFalso usuarios) =>
        new(new FecharSemanaValidator(), _fechamentos, _consultas, usuarios, usuario, new RelogioFixo(), _auditoria, _uow, _cache);

    private ReabrirSemanaHandler Reabrir(IUsuarioAtual usuario) =>
        new(new ReabrirSemanaValidator(), _fechamentos, usuario, new RelogioFixo(), _auditoria, _uow, _cache);

    private FechamentoSemanal SemanaFechada(Guid profissionalId, DateOnly? semana = null)
    {
        var f = FechamentoSemanal.Criar(profissionalId, semana ?? SemanaEncerrada);
        f.Fechar(Guid.NewGuid(), DateTimeOffset.UtcNow, DepoisDeTudo);
        _fechamentos.Adicionar(f);
        return f;
    }

    private Atendimento Atendimento(Guid profissionalId, DateTimeOffset inicio)
    {
        var a = Domain.Atendimentos.Atendimento.RegistrarAvulso(_aluno.Id, profissionalId, IntervaloHorario.Criar(inicio, inicio.AddHours(1)),
            TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, new DadosClinicos("Demanda", null, null, null),
            "Atendimento sem agenda", profissionalId, inicio.AddHours(2));
        _atendimentos.Adicionar(a);
        return a;
    }

    private CorrigirAtendimentoHandler CorrigirAtendimento(IUsuarioAtual usuario) =>
        new(new CorrigirAtendimentoValidator(), _atendimentos, _agenda, usuario, new RelogioFixo(), _auditoria, _uow, _cache,
            new ServicoEscala(new EscalaRepositoryEmMemoria(), new FeriadoRepositoryEmMemoria(), new RelogioFixo()), Servico());

    private static CorrigirAtendimentoCommand Correcao(Guid id, DateTimeOffset inicio) =>
        new(id, EstadoAtendimento.Realizado, TipoAtendimento.Devolutiva, inicio, inicio.AddHours(1), "Demanda revisada", null, null,
            null, null, "v1");

    [Fact]
    public async Task Profissional_fecha_a_propria_semana()
    {
        var usuario = Profissional();
        await Fechar(usuario).Handle(new FecharSemanaCommand(usuario.Id, SemanaEncerrada, false), TestContext.Current.CancellationToken);

        var f = Assert.Single(_fechamentos.Fechamentos);
        Assert.True(f.Fechada);
        Assert.Equal(usuario.Id, Assert.Single(f.Eventos).AutorId);
        Assert.Contains(("producao.semana.fechar", "FechamentoSemanal", (Guid?)f.Id), _auditoria.Registros);
        Assert.Equal(1, _cache.Invalidacoes);
    }

    [Fact]
    public async Task Coordenacao_fecha_semana_de_profissional_ativo()
    {
        var alvo = Guid.NewGuid();
        await Fechar(Coordenacao(), alvo).Handle(new FecharSemanaCommand(alvo, SemanaEncerrada, false), TestContext.Current.CancellationToken);
        Assert.True(Assert.Single(_fechamentos.Fechamentos).Fechada);
    }

    [Fact]
    public async Task Coordenacao_fecha_semana_de_quem_saiu_da_equipe_mas_tem_producao_nela()
    {
        // A consulta da produção devolve quem tem fatos na semana, qualquer que seja o perfil atual (ex.: virou Gestão).
        var exProfissional = Guid.NewGuid();
        var usuarios = new UsuarioQueriesFalso(exProfissional);
        usuarios.Usuarios.Add((exProfissional, Perfis.Gestao, null, true));

        await Fechar(Coordenacao(), usuarios).Handle(new FecharSemanaCommand(exProfissional, SemanaEncerrada, false),
            TestContext.Current.CancellationToken);

        Assert.True(Assert.Single(_fechamentos.Fechamentos).Fechada);
    }

    [Fact]
    public async Task Coordenacao_fecha_semana_de_profissional_inativo_mas_nao_de_outro_perfil()
    {
        var (inativo, sip) = (Guid.NewGuid(), Guid.NewGuid());
        var handler = Fechar(Coordenacao(), [(inativo, Perfis.ProfissionalNap, false), (sip, Perfis.Sip, true)]);

        await handler.Handle(new FecharSemanaCommand(inativo, SemanaEncerrada, false), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new FecharSemanaCommand(sip, SemanaEncerrada, false), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new FecharSemanaCommand(Guid.NewGuid(), SemanaEncerrada, false), TestContext.Current.CancellationToken));

        Assert.Equal(inativo, Assert.Single(_fechamentos.Fechamentos).ProfissionalId);
    }

    [Fact]
    public async Task Profissional_nao_fecha_semana_de_outro()
    {
        var outro = Guid.NewGuid();
        await Assert.ThrowsAsync<ForbiddenException>(() => Fechar(Profissional(), outro).Handle(
            new FecharSemanaCommand(outro, SemanaEncerrada, false), TestContext.Current.CancellationToken));
        Assert.Empty(_fechamentos.Fechamentos);
    }

    [Fact]
    public async Task Fechar_com_pendencias_sem_confirmar_retorna_422()
    {
        var usuario = Profissional();
        _consultas.Pendentes = 3;
        var ex = await Assert.ThrowsAsync<DomainException>(() => Fechar(usuario).Handle(
            new FecharSemanaCommand(usuario.Id, SemanaEncerrada, false), TestContext.Current.CancellationToken));
        Assert.Equal("A semana tem 3 pendências de classificação.", ex.Message);
        Assert.Empty(_fechamentos.Fechamentos);

        await Fechar(usuario).Handle(new FecharSemanaCommand(usuario.Id, SemanaEncerrada, true), TestContext.Current.CancellationToken);
        Assert.True(Assert.Single(_fechamentos.Fechamentos).Fechada);
    }

    [Fact]
    public async Task Fechar_com_compromisso_em_andamento_retorna_422_mesmo_confirmando()
    {
        var usuario = Profissional();
        _consultas.EmAndamento = 1;
        var ex = await Assert.ThrowsAsync<DomainException>(() => Fechar(usuario).Handle(
            new FecharSemanaCommand(usuario.Id, SemanaEncerrada, true), TestContext.Current.CancellationToken));
        Assert.Equal(FecharSemanaHandler.MensagemEmAndamento, ex.Message);
        Assert.Empty(_fechamentos.Fechamentos);
    }

    [Fact]
    public async Task Semana_ainda_nao_encerrada_nao_fecha()
    {
        var usuario = Profissional();
        var ex = await Assert.ThrowsAsync<DomainException>(() => Fechar(usuario).Handle(
            new FecharSemanaCommand(usuario.Id, new DateOnly(2026, 9, 28), false), TestContext.Current.CancellationToken));
        Assert.Equal("Só é possível fechar uma semana já encerrada.", ex.Message);
    }

    [Fact]
    public async Task Profissional_nao_reabre()
    {
        var usuario = Profissional();
        SemanaFechada(usuario.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => Reabrir(usuario).Handle(
            new ReabrirSemanaCommand(usuario.Id, SemanaEncerrada, "Preciso corrigir", "v1"), TestContext.Current.CancellationToken));
        Assert.True(_fechamentos.Fechamentos.Single().Fechada);
    }

    [Fact]
    public async Task Coordenacao_reabre_com_justificativa()
    {
        var profissional = Guid.NewGuid();
        var f = SemanaFechada(profissional);
        var coord = Coordenacao();

        await Assert.ThrowsAsync<ValidationException>(() => Reabrir(coord).Handle(
            new ReabrirSemanaCommand(profissional, SemanaEncerrada, "ab", "v1"), TestContext.Current.CancellationToken));
        await Reabrir(coord).Handle(new ReabrirSemanaCommand(profissional, SemanaEncerrada, "Correção de lançamento", "v1"),
            TestContext.Current.CancellationToken);

        Assert.False(f.Fechada);
        Assert.Equal([TipoEventoFechamento.Fechamento, TipoEventoFechamento.Reabertura], f.Eventos.Select(e => e.Tipo));
        Assert.Contains("v1", _uow.VersoesExigidas);
        Assert.Contains(("producao.semana.reabrir", "FechamentoSemanal", (Guid?)f.Id), _auditoria.Registros);
    }

    [Fact]
    public async Task Semana_fechada_bloqueia_correcao_de_atendimento()
    {
        var usuario = Profissional();
        SemanaFechada(usuario.Id);
        var a = Atendimento(usuario.Id, NaSemanaFechada);

        var ex = await Assert.ThrowsAsync<DomainException>(() => CorrigirAtendimento(usuario).Handle(
            Correcao(a.Id, NaSemanaFechada), TestContext.Current.CancellationToken));
        Assert.Equal("A semana está fechada; peça a reabertura à coordenação.", ex.Message);
        Assert.Equal(0, _uow.Salvamentos);
    }

    [Fact]
    public async Task Semana_fechada_bloqueia_mover_registro_para_dentro_dela()
    {
        var usuario = Profissional();
        SemanaFechada(usuario.Id);
        var a = Atendimento(usuario.Id, NaSemanaAberta);

        var ex = await Assert.ThrowsAsync<DomainException>(() => CorrigirAtendimento(usuario).Handle(
            Correcao(a.Id, NaSemanaFechada), TestContext.Current.CancellationToken));
        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);
        Assert.Equal(NaSemanaAberta, a.Intervalo.Inicio);
    }

    [Fact]
    public async Task Semana_fechada_bloqueia_atividade_avulsa()
    {
        var usuario = Profissional();
        SemanaFechada(usuario.Id);
        var handler = new RegistrarAtividadeHandler(new RegistrarAtividadeValidator(), _atividades, _agenda, new CursoQueriesFalso(),
            new UsuarioQueriesFalso(), usuario, new RelogioFixo(), _auditoria, _uow, _cache, Servico(),
            new ServicoEscala(new EscalaRepositoryEmMemoria(), new FeriadoRepositoryEmMemoria(), new RelogioFixo()));

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new RegistrarAtividadeCommand(null, null, NaSemanaFechada,
            NaSemanaFechada.AddHours(1), null, TipoAtividade.RodaConversa, "Roda de conversa", "Atividade sem agendamento prévio"),
            TestContext.Current.CancellationToken));
        Assert.Empty(_atividades.Atividades);
    }

    [Fact]
    public async Task Semana_fechada_bloqueia_cancelar_compromisso_terminado()
    {
        var usuario = Profissional();
        SemanaFechada(usuario.Id);
        var c = Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião", IntervaloHorario.Criar(NaSemanaFechada, NaSemanaFechada.AddHours(1)),
            usuario.Id, null, null, usuario.Id);
        _agenda.Adicionar(c);
        var handler = new CancelarCompromissoHandler(new CancelarCompromissoValidator(), _agenda, usuario, new RelogioFixo(), _auditoria,
            _uow, _cache, Servico());

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new CancelarCompromissoCommand(c.Id, "Não ocorreu", "v1"),
            TestContext.Current.CancellationToken));
        Assert.Equal(EstadoCompromisso.Agendado, c.Estado);
    }

    [Fact]
    public async Task Intervalo_que_termina_a_meia_noite_de_segunda_nao_toca_a_semana_fechada_que_comeca()
    {
        // Fuso do RelogioFixo é UTC. Semana de 14/09 aberta; a de 21/09 fechada.
        var usuario = Profissional();
        SemanaFechada(usuario.Id);
        var domingo = new DateTimeOffset(2026, 9, 20, 22, 0, 0, TimeSpan.Zero);

        await Servico().ExigirSemanaAbertaAsync(usuario.Id, IntervaloHorario.Criar(domingo, domingo.AddHours(2)), TestContext.Current.CancellationToken);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Servico().ExigirSemanaAbertaAsync(usuario.Id, IntervaloHorario.Criar(domingo, domingo.AddHours(2).AddMinutes(1)), TestContext.Current.CancellationToken));
        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);
    }

    [Fact]
    public async Task Semana_fechada_bloqueia_cancelar_bloqueio_em_andamento_mas_nao_reuniao_em_andamento()
    {
        // Relógio fixo: 02/10 15:00Z. A semana de 28/09 é fechada só aqui (DepoisDeTudo); os dois começaram às 10h e vão até as 20h.
        var usuario = Profissional();
        SemanaFechada(usuario.Id, new DateOnly(2026, 9, 28));
        var inicio = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        var bloqueio = Compromisso.Criar(TipoCompromisso.Bloqueio, "Bloqueio", IntervaloHorario.Criar(inicio, inicio.AddHours(10)), usuario.Id, null, null, usuario.Id);
        var reuniao = Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião", IntervaloHorario.Criar(inicio, inicio.AddHours(10)), usuario.Id, null, null, usuario.Id);
        _agenda.Adicionar(bloqueio);
        _agenda.Adicionar(reuniao);
        var handler = new CancelarCompromissoHandler(new CancelarCompromissoValidator(), _agenda, usuario, new RelogioFixo(), _auditoria,
            _uow, _cache, Servico());

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new CancelarCompromissoCommand(bloqueio.Id, "Não precisa mais", "v1"),
            TestContext.Current.CancellationToken));
        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);
        Assert.Equal(EstadoCompromisso.Agendado, bloqueio.Estado);

        await handler.Handle(new CancelarCompromissoCommand(reuniao.Id, "Não ocorreu", "v1"), TestContext.Current.CancellationToken);
        Assert.Equal(EstadoCompromisso.Cancelado, reuniao.Estado);
    }

    [Fact]
    public async Task Listar_semanas_do_profissional_ignora_filtro_de_outro()
    {
        var usuario = Profissional();
        var queries = new UsuarioQueriesFalso();
        queries.Usuarios.Add((usuario.Id, usuario.Perfil, null, true));
        var handler = new ListarSemanasHandler(new ListarSemanasValidator(), _consultas, queries, usuario, new RelogioFixo());

        await handler.Handle(new ListarSemanasQuery(Guid.NewGuid(), SemanaEncerrada, SemanaEncerrada.AddDays(20)),
            TestContext.Current.CancellationToken);

        Assert.Equal(usuario.Id, Assert.Single(_consultas.ProfissionaisRecebidos!).Id);
    }

    [Fact]
    public async Task Coordenacao_lista_semanas_de_todos_os_profissionais_ativos()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var handler = new ListarSemanasHandler(new ListarSemanasValidator(), _consultas, new UsuarioQueriesFalso(a, b), Coordenacao(), new RelogioFixo());

        await handler.Handle(new ListarSemanasQuery(null, SemanaEncerrada, SemanaEncerrada.AddDays(6)), TestContext.Current.CancellationToken);
        Assert.Equal(2, _consultas.ProfissionaisRecebidos!.Count);

        await handler.Handle(new ListarSemanasQuery(a, SemanaEncerrada, SemanaEncerrada.AddDays(6)), TestContext.Current.CancellationToken);
        Assert.Equal(a, Assert.Single(_consultas.ProfissionaisRecebidos!).Id);
    }

    [Fact]
    public async Task Janela_dos_profissionais_vai_ate_o_domingo_da_semana_do_fim()
    {
        var queries = new UsuarioQueriesFalso(Guid.NewGuid());
        var handler = new ListarSemanasHandler(new ListarSemanasValidator(), _consultas, queries, Coordenacao(), new RelogioFixo());

        // Fim numa quarta: fato de profissional inativo no domingo seguinte ainda pertence à semana listada.
        await handler.Handle(new ListarSemanasQuery(null, SemanaEncerrada, SemanaEncerrada.AddDays(2)), TestContext.Current.CancellationToken);

        Assert.Equal((SemanaEncerrada, SemanaEncerrada.AddDays(6)), queries.JanelaRecebida);
    }

    private Compromisso Reuniao(Guid profissionalId, DateTimeOffset inicio, TipoCompromisso tipo = TipoCompromisso.ReuniaoEquipe)
    {
        var c = Compromisso.Criar(tipo, "Reunião", IntervaloHorario.Criar(inicio, inicio.AddHours(1)), profissionalId, null, null,
            profissionalId);
        _agenda.Adicionar(c);
        return c;
    }

    private ServicoEscala Escala() => new(new EscalaRepositoryEmMemoria(), new FeriadoRepositoryEmMemoria(), new RelogioFixo());

    [Fact]
    public async Task Semana_fechada_bloqueia_remarcar_pendente_para_fora_dela()
    {
        var usuario = Profissional();
        SemanaFechada(usuario.Id);
        var c = Reuniao(usuario.Id, NaSemanaFechada);
        var handler = new RemarcarCompromissoHandler(new RemarcarCompromissoValidator(), _agenda, usuario, _auditoria,
            _uow, _cache, Escala(), Servico());

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new RemarcarCompromissoCommand(c.Id, NaSemanaAberta, NaSemanaAberta.AddHours(1), "v1"), TestContext.Current.CancellationToken));
        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);
        Assert.Equal(EstadoCompromisso.Agendado, c.Estado);
    }

    [Fact]
    public async Task Semana_fechada_bloqueia_criar_bloqueio_datado_nela()
    {
        var usuario = Profissional();
        SemanaFechada(usuario.Id);
        var handler = new CriarCompromissoHandler(new CriarCompromissoValidator(), _agenda, _alunos, new UsuarioQueriesFalso(), usuario,
            _auditoria, _uow, _cache, Escala(), Servico());

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new CriarCompromissoCommand(TipoCompromisso.Bloqueio,
            "Bloqueio", NaSemanaFechada, NaSemanaFechada.AddHours(1), null, null, null), TestContext.Current.CancellationToken));
        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);
        Assert.Empty(_agenda.Compromissos);
    }

    [Fact]
    public async Task Semana_fechada_bloqueia_bloqueio_que_comeca_na_semana_anterior_e_entra_nela()
    {
        // Domingo 22h até segunda 3h: o início cai na semana aberta, mas a segunda (fechada) perde capacidade.
        var usuario = Profissional();
        SemanaFechada(usuario.Id);
        var domingo = new DateTimeOffset(2026, 9, 20, 22, 0, 0, TimeSpan.Zero);
        var handler = new CriarCompromissoHandler(new CriarCompromissoValidator(), _agenda, _alunos, new UsuarioQueriesFalso(), usuario,
            _auditoria, _uow, _cache, Escala(), Servico());

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new CriarCompromissoCommand(TipoCompromisso.Bloqueio,
            "Bloqueio", domingo, domingo.AddHours(5), null, null, null), TestContext.Current.CancellationToken));
        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);
        Assert.Empty(_agenda.Compromissos);
    }

    private RegistrarRealizacaoHandler Realizacao(IUsuarioAtual usuario) =>
        new(new RegistrarRealizacaoValidator(), _agenda, usuario, new RelogioFixo(), _auditoria, _uow, _cache, Servico(), Escala());

    [Fact]
    public async Task Realizacao_e_bloqueada_se_so_a_data_efetiva_cai_em_semana_fechada()
    {
        var usuario = Profissional();
        SemanaFechada(usuario.Id);
        var c = Reuniao(usuario.Id, NaSemanaAberta);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Realizacao(usuario).Handle(
            new RegistrarRealizacaoCommand(c.Id, "v1", NaSemanaFechada, NaSemanaFechada.AddHours(1)), TestContext.Current.CancellationToken));
        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);
        Assert.Equal(EstadoCompromisso.Agendado, c.Estado);
    }

    [Fact]
    public async Task Realizacao_e_bloqueada_se_so_a_data_agendada_cai_em_semana_fechada()
    {
        var usuario = Profissional();
        SemanaFechada(usuario.Id);
        var c = Reuniao(usuario.Id, NaSemanaFechada);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Realizacao(usuario).Handle(
            new RegistrarRealizacaoCommand(c.Id, "v1", NaSemanaAberta, NaSemanaAberta.AddHours(1)), TestContext.Current.CancellationToken));
        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);
        Assert.Equal(EstadoCompromisso.Agendado, c.Estado);
    }

    [Fact]
    public async Task Semana_fechada_bloqueia_registrar_atendimento_do_compromisso()
    {
        var usuario = Profissional();
        SemanaFechada(usuario.Id);
        var c = Compromisso.Criar(TipoCompromisso.Atendimento, "Atendimento", IntervaloHorario.Criar(NaSemanaFechada, NaSemanaFechada.AddHours(1)),
            usuario.Id, _aluno.Id, null, usuario.Id);
        _agenda.Adicionar(c);
        var handler = new RegistrarAtendimentoHandler(new RegistrarAtendimentoValidator(), _atendimentos, _agenda, _alunos,
            new UsuarioQueriesFalso(), usuario, new RelogioFixo(), _auditoria, _uow, _cache, Escala(), Servico());

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new RegistrarAtendimentoCommand(c.Id, null, null, null, null,
            TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, "Demanda", null, null, null, null),
            TestContext.Current.CancellationToken));
        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);
        Assert.Empty(_atendimentos.Atendimentos);
    }

    [Fact]
    public async Task Semana_fechada_bloqueia_mover_atividade_para_dentro_dela()
    {
        var usuario = Profissional();
        SemanaFechada(usuario.Id);
        var a = AtividadeCurso.RegistrarAvulsa(usuario.Id, IntervaloHorario.Criar(NaSemanaAberta, NaSemanaAberta.AddHours(1)), null,
            TipoAtividade.RodaConversa, "Roda de conversa", "Atividade sem agendamento", usuario.Id, NaSemanaAberta.AddHours(2));
        _atividades.Adicionar(a);
        var handler = new CorrigirAtividadeHandler(new CorrigirAtividadeValidator(), _atividades, new AtividadeQueriesFalso(), _agenda,
            new CursoQueriesFalso(), usuario, new RelogioFixo(), _auditoria, _uow, _cache, Servico(),
            new ServicoEscala(new EscalaRepositoryEmMemoria(), new FeriadoRepositoryEmMemoria(), new RelogioFixo()));

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new CorrigirAtividadeCommand(a.Id, "v1", NaSemanaFechada,
            NaSemanaFechada.AddHours(1), null, TipoAtividade.RodaConversa, "Roda de conversa", "Ajuste de data"),
            TestContext.Current.CancellationToken));
        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);
    }

    [Fact]
    public async Task Guarda_toma_trava_compartilhada_e_fechar_e_reabrir_tomam_exclusiva()
    {
        var usuario = Profissional();
        var coord = Coordenacao();
        await Servico().ExigirSemanaAbertaAsync(usuario.Id, IntervaloHorario.Criar(NaSemanaAberta, NaSemanaAberta.AddHours(1)),
            TestContext.Current.CancellationToken);
        Assert.Equal([(usuario.Id, false)], _fechamentos.Bloqueios);

        _fechamentos.Bloqueios.Clear();
        await Fechar(usuario).Handle(new FecharSemanaCommand(usuario.Id, SemanaEncerrada, false), TestContext.Current.CancellationToken);
        Assert.Equal([(usuario.Id, true)], _fechamentos.Bloqueios);

        _fechamentos.Bloqueios.Clear();
        await Reabrir(coord).Handle(new ReabrirSemanaCommand(usuario.Id, SemanaEncerrada, "Correção de lançamento", "v1"),
            TestContext.Current.CancellationToken);
        Assert.Equal([(usuario.Id, true)], _fechamentos.Bloqueios);
    }
}
