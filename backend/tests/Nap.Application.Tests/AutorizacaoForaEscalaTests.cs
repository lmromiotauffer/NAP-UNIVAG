using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Agenda.Commands;
using Nap.Application.Features.Agenda.Queries;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Agenda;
using Nap.Domain.Comum;

namespace Nap.Application.Tests;

public class AutorizacaoForaEscalaTests
{
    // Fuso do RelogioFixo é UTC; agora = 2026-10-02 15:00Z. 2026-10-05 é segunda-feira (padrão 07–17h).
    private static readonly DateOnly Segunda = new(2026, 10, 5);
    private const string ForaDaEscala = "Fora da escala do profissional neste dia.";
    private readonly Guid _prof = Guid.NewGuid();
    private readonly Guid _outro = Guid.NewGuid();
    private readonly EscalaRepositoryEmMemoria _escalas = new();
    private readonly FeriadoRepositoryEmMemoria _feriados = new();
    private readonly AgendaRepositoryEmMemoria _agenda = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new();
    private readonly UsuarioAtualFalso _coord = new(Perfis.CoordenadorNap);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private ServicoEscala Servico() => new(_escalas, _feriados, new RelogioFixo());

    private static DateTimeOffset Em(DateOnly data, int hora, int minuto = 0) =>
        new(data.ToDateTime(new TimeOnly(hora, minuto)), TimeSpan.Zero);

    private CriarAutorizacaoForaEscalaHandler Criar(IUsuarioAtual? u = null) =>
        new(new CriarAutorizacaoForaEscalaValidator(), _escalas, new UsuarioQueriesFalso(_prof, _outro), u ?? _coord, new RelogioFixo(), _auditoria, _uow);

    private RevogarAutorizacaoForaEscalaHandler Revogar(IUsuarioAtual? u = null) =>
        new(_escalas, _agenda, u ?? _coord, new RelogioFixo(), _auditoria, _uow);

    private ListarAutorizacoesForaEscalaHandler Listar(IUsuarioAtual? u = null) =>
        new(new ListarAutorizacoesForaEscalaValidator(), _escalas, u ?? _coord, new RelogioFixo());

    private Task<Guid> Autorizar(Guid? prof = null, int de = 18, int ate = 21, DateOnly? dia = null) =>
        Criar().Handle(new CriarAutorizacaoForaEscalaCommand(prof ?? _prof, Em(dia ?? Segunda, de), Em(dia ?? Segunda, ate), "Plantão do vestibular"), Ct);

    [Fact]
    public async Task Atendimento_fora_da_escala_com_autorizacao_e_aceito()
    {
        await Autorizar();

        await Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda, 18), Ct);
        await Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda, 20, 59), Ct);
        // O início precisa cair dentro da autorização: o fim é exclusivo.
        await Assert.ThrowsAsync<DomainException>(() => Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda, 21), Ct));
    }

    [Fact]
    public async Task Sem_autorizacao_continua_422()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda, 18), Ct));
        Assert.Equal(ForaDaEscala, ex.Message);
    }

    [Fact]
    public async Task Autorizacao_de_outro_profissional_nao_vale()
    {
        await Autorizar(_outro);

        await Assert.ThrowsAsync<DomainException>(() => Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda, 18), Ct));
    }

    [Fact]
    public async Task Revogada_volta_a_recusar_e_mantem_o_registro_anterior()
    {
        var id = await Autorizar();
        await Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda, 19), Ct);

        await Revogar().Handle(new RevogarAutorizacaoForaEscalaCommand(id), Ct);

        await Assert.ThrowsAsync<DomainException>(() => Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda, 19), Ct));
        var pagina = await Listar().Handle(new ListarAutorizacoesForaEscalaQuery(_prof, null, null), Ct);
        var item = Assert.Single(pagina.Itens);
        Assert.NotNull(item.RevogadaEm);
        await Assert.ThrowsAsync<DomainException>(() => Revogar().Handle(new RevogarAutorizacaoForaEscalaCommand(id), Ct));
    }

    [Fact]
    public async Task Feriado_com_autorizacao_e_aceito()
    {
        _feriados.Feriados.Add(Feriado.Criar(Segunda, "Dia do teste"));

        await Assert.ThrowsAsync<DomainException>(() => Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda, 9), Ct));
        await Autorizar(de: 8, ate: 12);
        await Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda, 9), Ct);
    }

    [Theory]
    [InlineData(Perfis.ProfissionalNap)]
    [InlineData(Perfis.Gestao)]
    [InlineData(Perfis.Sip)]
    public async Task Quem_nao_e_coordenacao_recebe_403(string perfil)
    {
        var u = new UsuarioAtualFalso(perfil);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Criar(u).Handle(new CriarAutorizacaoForaEscalaCommand(_prof, Em(Segunda, 18), Em(Segunda, 20), "Plantão do vestibular"), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => Revogar(u).Handle(new RevogarAutorizacaoForaEscalaCommand(Guid.NewGuid()), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => Listar(u).Handle(new ListarAutorizacoesForaEscalaQuery(_prof, null, null), Ct));
    }

    [Fact]
    public async Task Criar_e_revogar_sao_auditados()
    {
        var id = await Autorizar();
        await Revogar().Handle(new RevogarAutorizacaoForaEscalaCommand(id), Ct);

        Assert.Equal([("escala.autorizacao.criar", "AutorizacaoForaEscala", (Guid?)id),
            ("escala.autorizacao.revogar", "AutorizacaoForaEscala", (Guid?)id)], _auditoria.Registros);
    }

    [Fact]
    public async Task Revogar_trava_a_agenda_do_profissional_e_inexistente_e_404()
    {
        var ordem = new List<string>();
        var agenda = new AgendaRepositoryEmMemoria(ordem);
        var id = await Autorizar();

        await new RevogarAutorizacaoForaEscalaHandler(_escalas, agenda, _coord, new RelogioFixo(), _auditoria, new UnidadeDeTrabalhoFalsa(ordem))
            .Handle(new RevogarAutorizacaoForaEscalaCommand(id), Ct);

        Assert.Equal(["transacao", "bloquear", "salvar"], ordem);
        await Assert.ThrowsAsync<NotFoundException>(() => Revogar().Handle(new RevogarAutorizacaoForaEscalaCommand(Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task Criar_valida_profissional_justificativa_e_duracao()
    {
        var naoProf = await Assert.ThrowsAsync<ValidationException>(() => Criar().Handle(
            new CriarAutorizacaoForaEscalaCommand(Guid.NewGuid(), Em(Segunda, 18), Em(Segunda, 20), "Plantão do vestibular"), Ct));
        Assert.Contains("profissionalId", naoProf.Erros.Keys);
        var curta = await Assert.ThrowsAsync<ValidationException>(() => Criar().Handle(
            new CriarAutorizacaoForaEscalaCommand(_prof, Em(Segunda, 18), Em(Segunda, 20), "abc"), Ct));
        Assert.Contains("justificativa", curta.Erros.Keys);
        var longa = await Assert.ThrowsAsync<ValidationException>(() => Criar().Handle(
            new CriarAutorizacaoForaEscalaCommand(_prof, Em(Segunda, 6), Em(Segunda, 19), "Plantão do vestibular"), Ct)); // 13 h
        Assert.Contains("fim", longa.Erros.Keys);
    }

    [Fact]
    public async Task Listar_filtra_por_profissional_e_periodo_com_paginacao()
    {
        await Autorizar(dia: Segunda);
        await Autorizar(dia: Segunda.AddDays(1));
        await Autorizar(dia: Segunda.AddDays(8));
        await Autorizar(_outro, dia: Segunda);

        var semana = await Listar().Handle(new ListarAutorizacoesForaEscalaQuery(_prof, Segunda, Segunda.AddDays(6)), Ct);
        Assert.Equal(2, semana.Total);
        var todas = await Listar().Handle(new ListarAutorizacoesForaEscalaQuery(null, null, null, 1, 3), Ct);
        Assert.Equal((4, 3), (todas.Total, todas.Itens.Count));
    }
}
