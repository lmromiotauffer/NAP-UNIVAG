using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Producao.Commands;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Agenda;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Producao;

namespace Nap.Application.Tests;

public class RealizacaoTests
{
    // Relógio fixo: 2026-10-02 15:00Z. Os intervalos abaixo estão no passado.
    private static readonly DateTimeOffset Inicio = new(2026, 10, 1, 13, 0, 0, TimeSpan.Zero);
    private readonly AgendaRepositoryEmMemoria _agenda = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly CacheConsultasFalso _cache = new();
    private readonly FechamentoRepositoryEmMemoria _fechamentos = new();
    private readonly EscalaRepositoryEmMemoria _escalas = new();
    private readonly FeriadoRepositoryEmMemoria _feriados = new();

    private RegistrarRealizacaoHandler Handler(IUsuarioAtual usuario) =>
        new(new RegistrarRealizacaoValidator(), _agenda, usuario, new RelogioFixo(), _auditoria, _uow, _cache,
            new ServicoFechamento(_fechamentos, new RelogioFixo()), new ServicoEscala(_escalas, _feriados, new RelogioFixo()));

    private CorrigirRealizacaoHandler Corrigir(IUsuarioAtual usuario) =>
        new(new CorrigirRealizacaoValidator(), _agenda, usuario, new RelogioFixo(), _auditoria, _uow, _cache,
            new ServicoFechamento(_fechamentos, new RelogioFixo()), new ServicoEscala(_escalas, _feriados, new RelogioFixo()));

    private Compromisso ReuniaoRealizada(Guid profissionalId, DateTimeOffset inicio)
    {
        var c = NovaReuniao(profissionalId, inicio);
        c.RegistrarRealizacao(c.Intervalo, profissionalId, inicio.AddDays(1));
        return c;
    }

    private void FecharSemana(Guid profissionalId, DateOnly segunda)
    {
        var f = FechamentoSemanal.Criar(profissionalId, segunda);
        f.Fechar(Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2026, 12, 1));
        _fechamentos.Adicionar(f);
    }

    private Compromisso NovaReuniao(Guid profissionalId, DateTimeOffset inicio)
    {
        var c = Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião de equipe", IntervaloHorario.Criar(inicio, inicio.AddHours(1)),
            profissionalId, null, null, profissionalId);
        _agenda.Adicionar(c);
        return c;
    }

    [Fact]
    public async Task Profissional_registra_realizacao_da_propria_reuniao()
    {
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var c = NovaReuniao(usuario.Id, Inicio);

        await Handler(usuario).Handle(new RegistrarRealizacaoCommand(c.Id, "v1", Inicio.AddMinutes(10), Inicio.AddMinutes(100)),
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoCompromisso.Concluido, c.Estado);
        Assert.Equal(Inicio.AddMinutes(10), c.RealizacaoInicio);
        Assert.Equal(Inicio.AddMinutes(100), c.RealizacaoFim);
        Assert.Contains("v1", _uow.VersoesExigidas);
        Assert.Contains(("compromisso.realizacao", "Compromisso", (Guid?)c.Id), _auditoria.Registros);
        Assert.Equal(1, _cache.Invalidacoes);
    }

    [Fact]
    public async Task Realizacao_sobreposta_retorna_conflito()
    {
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var c = NovaReuniao(usuario.Id, Inicio);
        NovaReuniao(usuario.Id, Inicio.AddHours(2));

        await Assert.ThrowsAsync<ConflictException>(() => Handler(usuario).Handle(
            new RegistrarRealizacaoCommand(c.Id, "v1", Inicio, Inicio.AddMinutes(150)), TestContext.Current.CancellationToken));
        Assert.Equal(EstadoCompromisso.Agendado, c.Estado);
    }

    // Domingo 27/09 20h em Cuiabá: fora da escala padrão.
    private static readonly DateTimeOffset DomingoNoite = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Realizacao_com_inicio_novo_fora_da_escala_e_recusada()
    {
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var c = NovaReuniao(usuario.Id, Inicio);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler(usuario).Handle(
            new RegistrarRealizacaoCommand(c.Id, "v1", DomingoNoite, DomingoNoite.AddHours(1)), TestContext.Current.CancellationToken));
        Assert.Equal("Fora da escala do profissional neste dia.", ex.Message);
        Assert.Equal(EstadoCompromisso.Agendado, c.Estado);
    }

    [Fact]
    public async Task Realizacao_com_inicio_novo_em_feriado_e_recusada()
    {
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var c = NovaReuniao(usuario.Id, Inicio);
        _feriados.Adicionar(Feriado.Criar(new DateOnly(2026, 10, 1), "Feriado municipal"));

        await Assert.ThrowsAsync<DomainException>(() => Handler(usuario).Handle(
            new RegistrarRealizacaoCommand(c.Id, "v1", Inicio.AddMinutes(10), Inicio.AddMinutes(70)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Realizacao_fora_da_escala_com_autorizacao_e_aceita()
    {
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var c = NovaReuniao(usuario.Id, Inicio);
        _escalas.AdicionarAutorizacao(AutorizacaoForaEscala.Criar(usuario.Id, IntervaloHorario.Criar(DomingoNoite, DomingoNoite.AddHours(2)),
            "Evento do curso", Guid.NewGuid(), DomingoNoite.AddDays(-1)));

        await Handler(usuario).Handle(new RegistrarRealizacaoCommand(c.Id, "v1", DomingoNoite, DomingoNoite.AddHours(1)),
            TestContext.Current.CancellationToken);
        Assert.Equal(EstadoCompromisso.Concluido, c.Estado);
    }

    [Fact]
    public async Task Correcao_com_inicio_novo_fora_da_escala_e_recusada()
    {
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var c = ReuniaoRealizada(usuario.Id, Inicio);

        await Assert.ThrowsAsync<DomainException>(() => Corrigir(usuario).Handle(
            new CorrigirRealizacaoCommand(c.Id, "v2", DomingoNoite, DomingoNoite.AddHours(1), "Horário anotado errado"),
            TestContext.Current.CancellationToken));
        Assert.Equal(Inicio, c.RealizacaoInicio);
    }

    [Fact]
    public async Task Profissional_nao_registra_realizacao_de_compromisso_de_outro()
    {
        var c = NovaReuniao(Guid.NewGuid(), Inicio);
        await Assert.ThrowsAsync<ForbiddenException>(() => Handler(new UsuarioAtualFalso(Perfis.ProfissionalNap)).Handle(
            new RegistrarRealizacaoCommand(c.Id, "v1", Inicio, Inicio.AddHours(1)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Profissional_corrige_a_realizacao_com_justificativa_e_historico()
    {
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var c = ReuniaoRealizada(usuario.Id, Inicio);

        await Corrigir(usuario).Handle(new CorrigirRealizacaoCommand(c.Id, "v2", Inicio.AddMinutes(30), Inicio.AddMinutes(90), "Horário anotado errado"),
            TestContext.Current.CancellationToken);

        Assert.Equal((Inicio.AddMinutes(30), Inicio.AddMinutes(90)), (c.RealizacaoInicio!.Value, c.RealizacaoFim!.Value));
        var correcao = Assert.Single(c.CorrecoesRealizacao);
        Assert.Equal((usuario.Id, Inicio, Inicio.AddHours(1)), (correcao.AutorId, correcao.InicioAnterior, correcao.FimAnterior));
        Assert.Contains("v2", _uow.VersoesExigidas);
        Assert.Contains(("compromisso.realizacao.correcao", "Compromisso", (Guid?)c.Id), _auditoria.Registros);
        Assert.Equal(1, _cache.Invalidacoes);
        // A trava de fechamento vem antes do conflito: as duas datas foram verificadas.
        Assert.Equal(2, _fechamentos.Bloqueios.Count);
    }

    [Fact]
    public async Task Correcao_sem_justificativa_e_invalida()
    {
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var c = ReuniaoRealizada(usuario.Id, Inicio);
        await Assert.ThrowsAsync<ValidationException>(() => Corrigir(usuario).Handle(
            new CorrigirRealizacaoCommand(c.Id, "v2", Inicio, Inicio.AddMinutes(90), "  "), TestContext.Current.CancellationToken));
        Assert.Empty(c.CorrecoesRealizacao);
    }

    [Fact]
    public async Task Correcao_com_semana_antiga_ou_nova_fechada_retorna_422()
    {
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var c = ReuniaoRealizada(usuario.Id, Inicio);
        var semanaAnterior = Inicio.AddDays(-7);
        FecharSemana(usuario.Id, new DateOnly(2026, 9, 21));

        // Mover para dentro da semana fechada.
        var ex = await Assert.ThrowsAsync<DomainException>(() => Corrigir(usuario).Handle(
            new CorrigirRealizacaoCommand(c.Id, "v2", semanaAnterior, semanaAnterior.AddHours(1), "Era na semana passada"),
            TestContext.Current.CancellationToken));
        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);

        // Tirar de dentro da semana fechada.
        var antiga = ReuniaoRealizada(usuario.Id, semanaAnterior.AddHours(3));
        ex = await Assert.ThrowsAsync<DomainException>(() => Corrigir(usuario).Handle(
            new CorrigirRealizacaoCommand(antiga.Id, "v2", Inicio.AddHours(3), Inicio.AddHours(4), "Era nesta semana"),
            TestContext.Current.CancellationToken));
        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);
        Assert.Empty(c.CorrecoesRealizacao);
        Assert.Empty(antiga.CorrecoesRealizacao);
    }

    [Fact]
    public async Task Correcao_sobreposta_retorna_conflito()
    {
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var c = ReuniaoRealizada(usuario.Id, Inicio);
        NovaReuniao(usuario.Id, Inicio.AddHours(2));

        await Assert.ThrowsAsync<ConflictException>(() => Corrigir(usuario).Handle(
            new CorrigirRealizacaoCommand(c.Id, "v2", Inicio, Inicio.AddMinutes(150), "Durou mais"), TestContext.Current.CancellationToken));
        Assert.Equal(Inicio.AddHours(1), c.RealizacaoFim);
    }

    [Fact]
    public async Task So_o_responsavel_ou_a_coordenacao_corrigem()
    {
        var c = ReuniaoRealizada(Guid.NewGuid(), Inicio);
        await Assert.ThrowsAsync<ForbiddenException>(() => Corrigir(new UsuarioAtualFalso(Perfis.ProfissionalNap)).Handle(
            new CorrigirRealizacaoCommand(c.Id, "v2", Inicio, Inicio.AddMinutes(30), "Horário errado"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ForbiddenException>(() => Corrigir(new UsuarioAtualFalso(Perfis.Gestao)).Handle(
            new CorrigirRealizacaoCommand(c.Id, "v2", Inicio, Inicio.AddMinutes(30), "Horário errado"), TestContext.Current.CancellationToken));

        await Corrigir(new UsuarioAtualFalso(Perfis.CoordenadorNap)).Handle(
            new CorrigirRealizacaoCommand(c.Id, "v2", Inicio, Inicio.AddMinutes(30), "Horário errado"), TestContext.Current.CancellationToken);
        Assert.Single(c.CorrecoesRealizacao);
    }
}
