using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Atendimentos;
using Nap.Application.Features.Atendimentos.Commands;
using Nap.Application.Features.Atendimentos.Queries;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Agenda;
using Nap.Domain.Alunos;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Tests;

public class AtendimentosTests
{
    // Relógio fixo: 2026-10-02 15:00Z. Intervalos abaixo estão no passado.
    private static readonly DateTimeOffset Inicio = new(2026, 10, 1, 13, 0, 0, TimeSpan.Zero);
    private readonly List<string> _ordem = [];
    private readonly AgendaRepositoryEmMemoria _agenda;
    private readonly AtendimentoRepositoryEmMemoria _repo = new();
    private readonly AlunoRepositoryEmMemoria _alunos = new();
    private readonly UnidadeDeTrabalhoFalsa _uow;
    private readonly CacheConsultasFalso _cache;
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly EscalaRepositoryEmMemoria _escalas = new();
    private readonly FeriadoRepositoryEmMemoria _feriados = new();
    private readonly FechamentoRepositoryEmMemoria _fechamentos = new();
    private readonly Aluno _aluno = Aluno.Criar("Maria da Silva", Matricula.Criar("2024001"), Guid.NewGuid(), 3,
        new DateOnly(2026, 8, 1), Guid.NewGuid(), null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

    public AtendimentosTests()
    {
        _agenda = new(_ordem, _repo.Atendimentos);
        _uow = new(_ordem);
        _cache = new(_ordem);
        _alunos.Adicionar(_aluno);
    }

    private RegistrarAtendimentoHandler Registrar(IUsuarioAtual usuario, params Guid[] ativos) =>
        new(new RegistrarAtendimentoValidator(), _repo, _agenda, _alunos, new UsuarioQueriesFalso(ativos), usuario,
            new RelogioFixo(), _auditoria, _uow, _cache, Escala(), Fechamento());

    private ServicoEscala Escala() => new(_escalas, _feriados, new RelogioFixo());

    private ServicoFechamento Fechamento() => new(_fechamentos, new RelogioFixo());

    private CorrigirAtendimentoHandler Corrigir(IUsuarioAtual usuario) =>
        new(new CorrigirAtendimentoValidator(), _repo, _agenda, usuario, new RelogioFixo(), _auditoria, _uow, _cache, Escala(), Fechamento());

    private static UsuarioAtualFalso Profissional() => new(Perfis.ProfissionalNap);

    private Compromisso NovoCompromisso(Guid profissionalId)
    {
        var c = Compromisso.Criar(TipoCompromisso.Atendimento, "Atendimento", IntervaloHorario.Criar(Inicio, Inicio.AddHours(1)),
            profissionalId, _aluno.Id, null, profissionalId);
        _agenda.Adicionar(c);
        return c;
    }

    private static RegistrarAtendimentoCommand DaAgenda(Guid compromissoId) =>
        new(compromissoId, null, null, null, null, TipoAtendimento.Psicopedagogico, EstadoAtendimento.Realizado,
            "Dificuldade de leitura", "obs", null, null, null);

    private RegistrarAtendimentoCommand Avulso(Guid? profissional = null) =>
        new(null, _aluno.Id, profissional, Inicio, Inicio.AddHours(1), TipoAtendimento.Acompanhamento,
            EstadoAtendimento.Realizado, "Demanda", null, null, null, "Aluno procurou o núcleo sem agendamento");

    private Atendimento AtendimentoExistente(Guid profissionalId)
    {
        var a = Atendimento.RegistrarAvulso(_aluno.Id, profissionalId, IntervaloHorario.Criar(Inicio, Inicio.AddHours(1)),
            TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, new DadosClinicos("Demanda", "Privado", null, null),
            "Atendimento sem agenda", profissionalId, Inicio.AddHours(2));
        _repo.Adicionar(a);
        return a;
    }

    private CorrigirAtendimentoCommand Correcao(Guid id, string? justificativa) =>
        new(id, EstadoAtendimento.Realizado, TipoAtendimento.Devolutiva, Inicio, Inicio.AddHours(1), "Demanda revisada", null,
            null, null, justificativa, "v1");

    [Fact]
    public async Task Registro_de_agendamento_invalida_o_painel_depois_de_salvar()
    {
        var profissional = Profissional();
        var c = NovoCompromisso(profissional.Id);
        _ordem.Clear();
        await Registrar(profissional).Handle(DaAgenda(c.Id), TestContext.Current.CancellationToken);
        Assert.Equal(["transacao", "salvar", "invalidar"], _ordem.Where(o => o is "transacao" or "salvar" or "invalidar"));
    }

    [Fact]
    public async Task Registro_avulso_salva_na_transacao_e_invalida_o_painel_depois()
    {
        _ordem.Clear();
        await Registrar(Profissional()).Handle(Avulso(), TestContext.Current.CancellationToken);
        Assert.Equal("invalidar", _ordem[^1]);
        Assert.True(_ordem.IndexOf("transacao") < _ordem.IndexOf("salvar"));
        Assert.True(_ordem.IndexOf("salvar") < _ordem.IndexOf("invalidar"));
    }

    [Fact]
    public async Task Correcao_invalida_o_painel_depois_de_salvar()
    {
        var profissional = Profissional();
        var a = AtendimentoExistente(profissional.Id);
        _ordem.Clear();
        await Corrigir(profissional).Handle(Correcao(a.Id, null), TestContext.Current.CancellationToken);
        Assert.Equal("invalidar", _ordem[^1]);
        Assert.True(_ordem.IndexOf("salvar") < _ordem.IndexOf("invalidar"));
    }

    [Fact]
    public async Task Agendamento_ja_registrado_gera_conflito()
    {
        var usuario = Profissional();
        var c = NovoCompromisso(usuario.Id);
        // o domínio conclui o compromisso ao registrar; a duplicidade vem do repositório (índice único na Infrastructure)
        _repo.Adicionar(Atendimento.RegistrarDeCompromisso(c, TipoAtendimento.Psicopedagogico, EstadoAtendimento.Realizado,
            new DadosClinicos("x", null, null, null), usuario.Id, Inicio.AddHours(2)));
        var ex = await Assert.ThrowsAsync<ConflictException>(() => Registrar(usuario).Handle(DaAgenda(c.Id), TestContext.Current.CancellationToken));
        Assert.Equal("Este agendamento já possui atendimento registrado", ex.Message);
    }

    [Fact]
    public async Task Registro_pela_agenda_conclui_o_compromisso_na_mesma_transacao()
    {
        var usuario = Profissional();
        var c = NovoCompromisso(usuario.Id);
        var id = await Registrar(usuario).Handle(DaAgenda(c.Id), TestContext.Current.CancellationToken);

        Assert.Equal(id, Assert.Single(_repo.Atendimentos).Id);
        Assert.Equal(EstadoCompromisso.Concluido, c.Estado);
        Assert.Equal(id, c.RegistroVinculadoId);
        Assert.Equal(["transacao", "salvar", "invalidar"], _ordem);
        Assert.Equal(("atendimento.registro", "Atendimento", (Guid?)id), Assert.Single(_auditoria.Registros));
    }

    [Fact]
    public async Task Registro_pela_agenda_de_compromisso_inexistente_gera_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Registrar(Profissional()).Handle(DaAgenda(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Profissional_nao_registra_compromisso_de_outro_mas_coordenacao_sim()
    {
        var c = NovoCompromisso(Guid.NewGuid());
        await Assert.ThrowsAsync<ForbiddenException>(() => Registrar(Profissional()).Handle(DaAgenda(c.Id), TestContext.Current.CancellationToken));
        await Registrar(new UsuarioAtualFalso(Perfis.CoordenadorNap)).Handle(DaAgenda(c.Id), TestContext.Current.CancellationToken);
        Assert.Single(_repo.Atendimentos);
    }

    [Fact]
    public async Task Avulso_bloqueia_e_checa_conflito_na_transacao_e_nao_cria_compromisso()
    {
        var usuario = Profissional();
        var id = await Registrar(usuario).Handle(Avulso(), TestContext.Current.CancellationToken);
        var a = Assert.Single(_repo.Atendimentos);
        Assert.Equal(id, a.Id);
        Assert.Equal(usuario.Id, a.ProfissionalId);
        Assert.Empty(_agenda.Compromissos);
        Assert.Equal(["transacao", "bloquear", "conflito", "salvar", "invalidar"], _ordem);
    }

    [Fact]
    public async Task Avulso_em_horario_conflitante_gera_conflito()
    {
        var usuario = Profissional();
        _agenda.Adicionar(Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião", IntervaloHorario.Criar(Inicio, Inicio.AddHours(1)),
            usuario.Id, null, null, usuario.Id));
        await Assert.ThrowsAsync<ConflictException>(() => Registrar(usuario).Handle(Avulso(), TestContext.Current.CancellationToken));
        Assert.Empty(_repo.Atendimentos);
    }

    [Fact]
    public async Task Avulso_exige_aluno_intervalo_e_justificativa()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Registrar(Profissional()).Handle(
            Avulso() with { AlunoId = null, Inicio = null, Fim = null, JustificativaAvulso = null }, TestContext.Current.CancellationToken));
        Assert.Contains("alunoId", ex.Erros.Keys);
        Assert.Contains("inicio", ex.Erros.Keys);
        Assert.Contains("fim", ex.Erros.Keys);
        Assert.Contains("justificativaAvulso", ex.Erros.Keys);
    }

    [Fact]
    public async Task Avulso_com_justificativa_curta_e_erro_de_campo_e_nao_do_dominio()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Registrar(Profissional()).Handle(
            Avulso() with { JustificativaAvulso = "n/a" }, TestContext.Current.CancellationToken));
        Assert.Contains("justificativaAvulso", ex.Erros.Keys);
    }

    [Fact]
    public async Task Avulso_para_outro_profissional_so_coordenacao_e_aluno_inexistente_gera_validacao()
    {
        var outro = Guid.NewGuid();
        await Assert.ThrowsAsync<ForbiddenException>(() => Registrar(Profissional(), outro).Handle(Avulso(outro), TestContext.Current.CancellationToken));

        var coord = Registrar(new UsuarioAtualFalso(Perfis.CoordenadorNap), outro);
        await coord.Handle(Avulso(outro), TestContext.Current.CancellationToken);
        Assert.Equal(outro, Assert.Single(_repo.Atendimentos).ProfissionalId);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => coord.Handle(Avulso(outro) with { AlunoId = Guid.NewGuid() }, TestContext.Current.CancellationToken));
        Assert.Contains("alunoId", ex.Erros.Keys);
    }

    [Fact]
    public async Task Profissional_nao_corrige_atendimento_de_outro()
    {
        var a = AtendimentoExistente(Guid.NewGuid());
        await Assert.ThrowsAsync<ForbiddenException>(() => Corrigir(Profissional()).Handle(Correcao(a.Id, "Justificativa válida"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Profissional_corrige_o_proprio_sem_justificativa()
    {
        var usuario = Profissional();
        var a = AtendimentoExistente(usuario.Id);
        await Corrigir(usuario).Handle(Correcao(a.Id, null), TestContext.Current.CancellationToken);
        Assert.Equal(TipoAtendimento.Devolutiva, a.Tipo);
        Assert.Single(a.Correcoes);
    }

    [Fact]
    public async Task Coordenacao_corrige_com_justificativa_e_auditoria()
    {
        var a = AtendimentoExistente(Guid.NewGuid());
        await Corrigir(new UsuarioAtualFalso(Perfis.CoordenadorNap)).Handle(Correcao(a.Id, "Ajuste solicitado pela equipe"), TestContext.Current.CancellationToken);
        var correcao = Assert.Single(a.Correcoes);
        Assert.Equal("Demanda", correcao.DadosAnteriores!.DemandaPrincipal);
        Assert.Equal(("atendimento.correcao", "Atendimento", (Guid?)a.Id), Assert.Single(_auditoria.Registros));
    }

    [Fact]
    public async Task Coordenacao_sem_justificativa_gera_validacao()
    {
        var a = AtendimentoExistente(Guid.NewGuid());
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Corrigir(new UsuarioAtualFalso(Perfis.Administrador)).Handle(Correcao(a.Id, "  "), TestContext.Current.CancellationToken));
        Assert.Contains("justificativa", ex.Erros.Keys);
        Assert.Empty(a.Correcoes);
    }

    [Fact]
    public async Task Corrigir_atendimento_com_versao_antiga_lanca_conflito_e_nao_salva()
    {
        var usuario = Profissional();
        var a = AtendimentoExistente(usuario.Id);
        _uow.VersaoAtual = "v2";
        var salvamentosAntes = _uow.Salvamentos;
        await Assert.ThrowsAsync<ConflictException>(() => Corrigir(usuario).Handle(Correcao(a.Id, null), TestContext.Current.CancellationToken));
        Assert.Equal(salvamentosAntes, _uow.Salvamentos);
        Assert.Empty(a.Correcoes);
    }

    [Fact]
    public async Task Correcao_para_horario_ocupado_gera_conflito()
    {
        var usuario = Profissional();
        var a = AtendimentoExistente(usuario.Id);
        _agenda.Adicionar(Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião",
            IntervaloHorario.Criar(Inicio.AddHours(3), Inicio.AddHours(4)), usuario.Id, null, null, usuario.Id));
        var cmd = Correcao(a.Id, null) with { Inicio = Inicio.AddHours(3), Fim = Inicio.AddHours(4) };
        await Assert.ThrowsAsync<ConflictException>(() => Corrigir(usuario).Handle(cmd, TestContext.Current.CancellationToken));
        Assert.Empty(a.Correcoes);
    }

    [Fact]
    public async Task Correcao_com_mesmo_horario_nao_checa_conflito()
    {
        var usuario = Profissional();
        var a = AtendimentoExistente(usuario.Id);
        await Corrigir(usuario).Handle(Correcao(a.Id, null), TestContext.Current.CancellationToken);
        Assert.Equal(["transacao", "salvar", "invalidar"], _ordem);
    }

    [Fact]
    public async Task Correcao_ignora_o_compromisso_vinculado_ao_proprio_atendimento()
    {
        var usuario = Profissional();
        var c = NovoCompromisso(usuario.Id);
        var id = await Registrar(usuario).Handle(DaAgenda(c.Id), TestContext.Current.CancellationToken);
        var cmd = Correcao(id, null) with { Inicio = Inicio.AddMinutes(30), Fim = Inicio.AddMinutes(90) };
        await Corrigir(usuario).Handle(cmd, TestContext.Current.CancellationToken);
        Assert.Single(_repo.Atendimentos.Single().Correcoes);
    }

    [Fact]
    public async Task Corrigir_por_perfil_fora_da_equipe_e_negado()
    {
        var a = AtendimentoExistente(Guid.NewGuid());
        await Assert.ThrowsAsync<ForbiddenException>(() => Corrigir(new UsuarioAtualFalso(Perfis.Gestao)).Handle(Correcao(a.Id, "Justificativa válida"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Corrigir_atendimento_inexistente_gera_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Corrigir(Profissional()).Handle(Correcao(Guid.NewGuid(), null), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(Perfis.CoordenadorCurso)]
    [InlineData(Perfis.Sip)]
    [InlineData(Perfis.Gestao)]
    public async Task Coordenador_de_curso_nao_acessa_atendimentos(string perfil)
    {
        var usuario = new UsuarioAtualFalso(perfil, Guid.NewGuid());
        var consultas = new AtendimentoQueriesFalso();
        var lista = new ListarAtendimentosHandler(new ListarAtendimentosValidator(), consultas, usuario);
        var detalhe = new ObterAtendimentoHandler(new ObterAtendimentoValidator(), consultas, usuario, _auditoria, _uow);
        await Assert.ThrowsAsync<ForbiddenException>(() => lista.Handle(new ListarAtendimentosQuery(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ForbiddenException>(() => detalhe.Handle(new ObterAtendimentoQuery(Guid.NewGuid()), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ForbiddenException>(() => Registrar(usuario).Handle(Avulso(), TestContext.Current.CancellationToken));
        Assert.Empty(_auditoria.Registros);
    }

    [Fact]
    public async Task Detalhe_audita_consulta_e_inexistente_gera_not_found()
    {
        var id = Guid.NewGuid();
        var consultas = new AtendimentoQueriesFalso
        {
            Detalhe = new AtendimentoDetalheDto(id, null, Inicio, Inicio.AddHours(1), 60, _aluno.Id, "Maria", Guid.NewGuid(), "Ana",
                TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, true, "j", new DadosClinicosDto("d", "obs", null, null), [], "AAAAAAAAB9E="),
        };
        var h = new ObterAtendimentoHandler(new ObterAtendimentoValidator(), consultas, Profissional(), _auditoria, _uow);
        Assert.Equal("obs", (await h.Handle(new ObterAtendimentoQuery(id), TestContext.Current.CancellationToken)).DadosClinicos!.ObservacoesPrivadas);
        Assert.Equal(("atendimento.consulta", "Atendimento", (Guid?)id), Assert.Single(_auditoria.Registros));
        await Assert.ThrowsAsync<NotFoundException>(() => h.Handle(new ObterAtendimentoQuery(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(100_001, 20)]
    public async Task Listagem_valida_paginacao(int pagina, int tamanho)
    {
        var h = new ListarAtendimentosHandler(new ListarAtendimentosValidator(), new AtendimentoQueriesFalso(), Profissional());
        await Assert.ThrowsAsync<ValidationException>(() => h.Handle(new ListarAtendimentosQuery(Pagina: pagina, Tamanho: tamanho), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Correcao_de_cancelado_para_realizado_no_mesmo_horario_checa_conflito()
    {
        var usuario = Profissional();
        var a = AtendimentoExistente(usuario.Id);
        await Corrigir(usuario).Handle(Correcao(a.Id, null) with { Estado = EstadoAtendimento.Cancelado }, TestContext.Current.CancellationToken);
        // Cancelado libera o horário, que é reaproveitado por outro compromisso.
        _agenda.Adicionar(Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião", IntervaloHorario.Criar(Inicio, Inicio.AddHours(1)),
            usuario.Id, null, null, usuario.Id));
        _ordem.Clear();

        await Assert.ThrowsAsync<ConflictException>(() => Corrigir(usuario).Handle(Correcao(a.Id, null), TestContext.Current.CancellationToken));
        Assert.Contains("conflito", _ordem);
        Assert.Equal(EstadoAtendimento.Cancelado, a.Estado);
    }

    [Fact]
    public async Task Correcao_para_cancelado_no_mesmo_horario_nao_checa_conflito()
    {
        var usuario = Profissional();
        var a = AtendimentoExistente(usuario.Id);
        await Corrigir(usuario).Handle(Correcao(a.Id, null) with { Estado = EstadoAtendimento.Cancelado }, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("conflito", _ordem);
    }

    [Fact]
    public async Task Avulso_e_correcao_em_fim_de_semana_sao_rejeitados()
    {
        var usuario = Profissional();
        var sabado = new DateTimeOffset(2026, 9, 26, 13, 0, 0, TimeSpan.Zero);
        var ex = await Assert.ThrowsAsync<DomainException>(() => Registrar(usuario).Handle(
            Avulso() with { Inicio = sabado, Fim = sabado.AddHours(1) }, TestContext.Current.CancellationToken));
        Assert.Equal("Fora da escala do profissional neste dia.", ex.Message);

        var a = AtendimentoExistente(usuario.Id);
        var domingo = sabado.AddDays(1);
        ex = await Assert.ThrowsAsync<DomainException>(() => Corrigir(usuario).Handle(
            Correcao(a.Id, null) with { Inicio = domingo, Fim = domingo.AddHours(1) }, TestContext.Current.CancellationToken));
        Assert.Equal("Fora da escala do profissional neste dia.", ex.Message);
        Assert.Empty(a.Correcoes);
        Assert.Single(_repo.Atendimentos);
    }

    [Fact]
    public async Task Correcao_so_do_texto_em_data_que_virou_feriado_e_permitida()
    {
        var usuario = Profissional();
        var a = AtendimentoExistente(usuario.Id);
        _feriados.Adicionar(Nap.Domain.Agenda.Feriado.Criar(DateOnly.FromDateTime(Inicio.UtcDateTime), "Ponto facultativo"));
        await Corrigir(usuario).Handle(Correcao(a.Id, null), TestContext.Current.CancellationToken);
        Assert.Single(a.Correcoes);
        // Só o término muda: o início (já registrado) não passa de novo pela escala.
        await Corrigir(usuario).Handle(Correcao(a.Id, null) with { Fim = a.Intervalo.Fim.AddMinutes(10) },
            TestContext.Current.CancellationToken);
        Assert.Equal(2, a.Correcoes.Count);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Corrigir(usuario).Handle(
            Correcao(a.Id, null) with { Inicio = Inicio.AddHours(1), Fim = Inicio.AddHours(2) }, TestContext.Current.CancellationToken));
        Assert.Equal("Esta data é feriado: Ponto facultativo.", ex.Message);
    }

    [Fact]
    public async Task Avulso_na_sexta_sem_atendimento_e_permitido()
    {
        var sexta = new DateTimeOffset(2026, 9, 25, 13, 0, 0, TimeSpan.Zero);
        await Registrar(Profissional()).Handle(Avulso() with { Inicio = sexta, Fim = sexta.AddHours(1) }, TestContext.Current.CancellationToken);
        Assert.Single(_repo.Atendimentos);
    }

    [Fact]
    public void Lista_nao_expoe_observacoes_privadas()
    {
        Assert.Null(typeof(AtendimentoListaDto).GetProperty("ObservacoesPrivadas"));
    }
}
