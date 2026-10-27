using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Atividades;
using Nap.Application.Features.Atividades.Commands;
using Nap.Application.Features.Atividades.Queries;
using Nap.Application.Features.Producao;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Agenda;
using Nap.Domain.Atividades;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Tests;

public class AtividadesTests
{
    // Relógio fixo: 2026-10-02 15:00Z. Intervalos abaixo estão no passado.
    private static readonly DateTimeOffset Inicio = new(2026, 10, 1, 13, 0, 0, TimeSpan.Zero);
    private readonly AgendaRepositoryEmMemoria _agenda = new();
    private readonly AtividadeRepositoryEmMemoria _repo = new();
    private readonly AtividadeQueriesFalso _consultas = new();
    private readonly FechamentoRepositoryEmMemoria _fechamentos = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly CacheConsultasFalso _cache = new();
    private readonly Guid _curso = Guid.NewGuid();

    private RegistrarAtividadeHandler Registrar(IUsuarioAtual usuario, params Guid[] profissionaisAtivos) =>
        new(new RegistrarAtividadeValidator(), _repo, _agenda, new CursoQueriesFalso(_curso),
            new UsuarioQueriesFalso(profissionaisAtivos), usuario, new RelogioFixo(), _auditoria, _uow, _cache, Fechamento(), Escala());

    private ServicoFechamento Fechamento() => new(_fechamentos, new RelogioFixo());

    private readonly EscalaRepositoryEmMemoria _escalas = new();

    private ServicoEscala Escala() => new(_escalas, new FeriadoRepositoryEmMemoria(), new RelogioFixo());

    private CorrigirAtividadeHandler Corrigir(IUsuarioAtual usuario, UnidadeDeTrabalhoFalsa? uow = null) =>
        new(new CorrigirAtividadeValidator(), _repo, _consultas, _agenda, new CursoQueriesFalso(_curso), usuario,
            new RelogioFixo(), _auditoria, uow ?? _uow, _cache, Fechamento(), Escala());

    private static UsuarioAtualFalso Profissional() => new(Perfis.ProfissionalNap);

    private Compromisso NovoCompromisso(Guid profissionalId, TipoCompromisso tipo = TipoCompromisso.AtividadeCurso)
    {
        var c = Compromisso.Criar(tipo, "Roda de conversa", IntervaloHorario.Criar(Inicio, Inicio.AddHours(1)), profissionalId,
            null, null, profissionalId);
        _agenda.Adicionar(c);
        return c;
    }

    private static RegistrarAtividadeCommand DaAgenda(Guid compromissoId, Guid? curso = null) =>
        new(compromissoId, null, Inicio, Inicio.AddHours(1), curso, TipoAtividade.RodaConversa, "Roda de conversa", null);

    private static RegistrarAtividadeCommand Avulsa(DateTimeOffset inicio, Guid? curso = null, Guid? profissional = null) =>
        new(null, profissional, inicio, inicio.AddHours(1), curso, TipoAtividade.ReuniaoPedagogica, "Reunião pedagógica",
            "Atividade sem agendamento prévio");

    private AtividadeCurso Existente(Guid profissionalId, Guid? curso = null)
    {
        var a = AtividadeCurso.RegistrarAvulsa(profissionalId, IntervaloHorario.Criar(Inicio, Inicio.AddHours(1)), curso,
            TipoAtividade.RodaConversa, "Roda de conversa", "Atividade sem agendamento", profissionalId, Inicio.AddHours(2));
        _repo.Adicionar(a);
        return a;
    }

    private static CorrigirAtividadeCommand Correcao(Guid id, string justificativa = "Ajuste de duração") =>
        new(id, "v1", Inicio, Inicio.AddHours(2), null, TipoAtividade.Outra, "Descrição revisada", justificativa);

    private static AtividadeDetalheDto Detalhe(Guid profissionalId, Guid? curso) =>
        new(Guid.NewGuid(), new DateOnly(2026, 10, 1), Turno.Manha, Inicio, Inicio.AddHours(1), 60, curso, null, profissionalId,
            "Prof", TipoAtividade.RodaConversa, "Roda", true, "v1", "Justificativa", null, [], false);

    [Fact]
    public async Task Profissional_registra_a_partir_do_proprio_compromisso()
    {
        var usuario = Profissional();
        var c = NovoCompromisso(usuario.Id);
        var id = await Registrar(usuario).Handle(DaAgenda(c.Id, _curso), TestContext.Current.CancellationToken);

        var atividade = Assert.Single(_repo.Atividades);
        Assert.Equal(id, atividade.Id);
        Assert.Equal(c.Id, atividade.CompromissoId);
        Assert.Equal(EstadoCompromisso.Concluido, c.Estado);
        Assert.Equal(id, c.RegistroVinculadoId);
        Assert.Contains(("atividade.registro", "AtividadeCurso", (Guid?)id), _auditoria.Registros);
        Assert.Equal(1, _cache.Invalidacoes);
    }

    [Fact]
    public async Task Profissional_nao_registra_compromisso_de_outro()
    {
        var c = NovoCompromisso(Guid.NewGuid());
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Registrar(Profissional()).Handle(DaAgenda(c.Id), TestContext.Current.CancellationToken));
        Assert.Empty(_repo.Atividades);
    }

    [Fact]
    public async Task Compromisso_ja_registrado_retorna_conflito()
    {
        var usuario = Profissional();
        var c = NovoCompromisso(usuario.Id);
        _repo.Adicionar(AtividadeCurso.RegistrarDeCompromisso(c, c.Intervalo, null, TipoAtividade.RodaConversa, "Roda", usuario.Id,
            Inicio.AddHours(2)));
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Registrar(usuario).Handle(DaAgenda(c.Id), TestContext.Current.CancellationToken));
        Assert.Equal("Este agendamento já possui atividade registrada.", ex.Message);
    }

    [Fact]
    public async Task Avulsa_fora_da_escala_exige_autorizacao()
    {
        var usuario = Profissional();
        var noturna = Inicio.AddHours(7);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Registrar(usuario).Handle(Avulsa(noturna), TestContext.Current.CancellationToken));
        Assert.Equal("Fora da escala do profissional neste dia.", ex.Message);
        Assert.Empty(_repo.Atividades);

        _escalas.Autorizacoes.Add(AutorizacaoForaEscala.Criar(usuario.Id, IntervaloHorario.Criar(noturna, noturna.AddHours(2)),
            "Atividade noturna do curso", Guid.NewGuid(), Inicio));
        var id = await Registrar(usuario).Handle(Avulsa(noturna), TestContext.Current.CancellationToken);
        Assert.Equal(id, Assert.Single(_repo.Atividades).Id);
    }

    [Fact]
    public async Task Avulsa_sobreposta_retorna_conflito()
    {
        var usuario = Profissional();
        NovoCompromisso(usuario.Id, TipoCompromisso.ReuniaoEquipe);
        await Assert.ThrowsAsync<ConflictException>(() =>
            Registrar(usuario).Handle(Avulsa(Inicio.AddMinutes(30)), TestContext.Current.CancellationToken));
        Assert.Empty(_repo.Atividades);
    }

    [Fact]
    public async Task Curso_inativo_no_registro_retorna_400()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Registrar(Profissional()).Handle(Avulsa(Inicio, Guid.NewGuid()), TestContext.Current.CancellationToken));
        Assert.Contains("cursoId", ex.Erros.Keys);
    }

    [Fact]
    public async Task Profissional_nao_corrige_atividade_de_outro()
    {
        var a = Existente(Guid.NewGuid());
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Corrigir(Profissional()).Handle(Correcao(a.Id), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Coordenacao_corrige_com_justificativa()
    {
        var a = Existente(Guid.NewGuid());
        var usuario = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        _consultas.Detalhes.Add(Detalhe(a.ProfissionalId, null) with { Id = a.Id });
        var dto = await Corrigir(usuario).Handle(Correcao(a.Id), TestContext.Current.CancellationToken);

        Assert.True(dto.PodeCorrigir);
        Assert.Equal("Descrição revisada", a.Descricao);
        Assert.Equal(TipoAtividade.Outra, a.Tipo);
        Assert.Single(a.Correcoes);
        Assert.Contains(("atividade.correcao", "AtividadeCurso", (Guid?)a.Id), _auditoria.Registros);
    }

    [Fact]
    public async Task Correcao_sem_justificativa_retorna_400_no_campo()
    {
        var usuario = Profissional();
        var a = Existente(usuario.Id);
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Corrigir(usuario).Handle(Correcao(a.Id, ""), TestContext.Current.CancellationToken));
        Assert.Contains("justificativa", ex.Erros.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Correcao_com_versao_antiga_retorna_conflito()
    {
        var usuario = Profissional();
        var a = Existente(usuario.Id);
        await Assert.ThrowsAsync<ConflictException>(() =>
            Corrigir(usuario, new UnidadeDeTrabalhoFalsa { VersaoAtual = "v2" }).Handle(Correcao(a.Id), TestContext.Current.CancellationToken));
        Assert.Empty(a.Correcoes);
    }

    [Fact]
    public async Task Correcao_mantem_curso_inativo_mas_troca_exige_ativo()
    {
        var inativo = Guid.NewGuid();
        var usuario = Profissional();
        var a = Existente(usuario.Id, inativo);
        _consultas.Detalhes.Add(Detalhe(usuario.Id, inativo) with { Id = a.Id });

        await Corrigir(usuario).Handle(Correcao(a.Id) with { CursoId = inativo }, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ValidationException>(() =>
            Corrigir(usuario).Handle(Correcao(a.Id) with { CursoId = Guid.NewGuid() }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Coordenador_de_curso_lista_so_o_proprio_curso()
    {
        var usuario = new UsuarioAtualFalso(Perfis.CoordenadorCurso, _curso);
        var handler = new ListarAtividadesHandler(new ListarAtividadesValidator(), _consultas, usuario);
        await handler.Handle(new ListarAtividadesQuery(CursoId: Guid.NewGuid(), Institucionais: true), TestContext.Current.CancellationToken);

        Assert.Equal(_curso, _consultas.FiltroRecebido!.CursoRecorte);
        Assert.Null(_consultas.FiltroRecebido.Institucionais);
    }

    [Fact]
    public async Task Coordenador_de_curso_nao_obtem_institucional()
    {
        var usuario = new UsuarioAtualFalso(Perfis.CoordenadorCurso, _curso);
        var institucional = Detalhe(Guid.NewGuid(), null);
        var doCurso = Detalhe(Guid.NewGuid(), _curso);
        _consultas.Detalhes.AddRange([institucional, doCurso]);
        var handler = new ObterAtividadeHandler(new ObterAtividadeValidator(), _consultas, usuario);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(institucional.Id), TestContext.Current.CancellationToken));
        var dto = await handler.Handle(new(doCurso.Id), TestContext.Current.CancellationToken);
        Assert.False(dto.PodeCorrigir);
    }

    [Fact]
    public async Task Sip_recebe_403()
    {
        var usuario = new UsuarioAtualFalso(Perfis.Sip);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ListarAtividadesHandler(new ListarAtividadesValidator(), _consultas, usuario)
                .Handle(new ListarAtividadesQuery(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ObterAtividadeHandler(new ObterAtividadeValidator(), _consultas, usuario)
                .Handle(new(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }
}
