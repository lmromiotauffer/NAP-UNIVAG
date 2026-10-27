using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Agenda.Commands;
using Nap.Application.Features.Agenda.Queries;
using Nap.Application.Features.Producao;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Agenda;
using Nap.Domain.Comum;
using Nap.Domain.Producao;

namespace Nap.Application.Tests;

public class EscalaTests
{
    // Fuso do RelogioFixo é UTC. 2026-10-05 é segunda-feira.
    private static readonly DateOnly Segunda = new(2026, 10, 5);
    private const string ForaDaEscala = "Fora da escala do profissional neste dia.";
    private readonly Guid _prof = Guid.NewGuid();
    private readonly EscalaRepositoryEmMemoria _escalas = new();
    private readonly FeriadoRepositoryEmMemoria _feriados = new();
    private readonly AgendaRepositoryEmMemoria _agenda = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new();
    private readonly UsuarioAtualFalso _coord = new(Perfis.CoordenadorNap);
    private readonly FechamentoRepositoryEmMemoria _fechamentos = new();

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private ServicoEscala Servico() => new(_escalas, _feriados, new RelogioFixo());

    private static DateTimeOffset Em(DateOnly data, int hora, int minuto = 0) =>
        new(data.ToDateTime(new TimeOnly(hora, minuto)), TimeSpan.Zero);

    private static FaixaEscalaInput Faixa(DayOfWeek dia, int de = 8, int ate = 12, bool sem = false) =>
        new(dia, new TimeOnly(de, 0), new TimeOnly(ate, 0), sem);

    private CriarEscalaHandler Criar(IUsuarioAtual? u = null) =>
        new(new CriarEscalaValidator(), _escalas, _agenda, new UsuarioQueriesFalso(_prof), u ?? _coord, _auditoria, _uow, new ServicoFechamento(_fechamentos, new RelogioFixo()));

    private AtualizarVigenciaEscalaHandler Atualizar() =>
        new(new AtualizarVigenciaEscalaValidator(), _escalas, _agenda, _coord, _auditoria, _uow, new ServicoFechamento(_fechamentos, new RelogioFixo()));

    private ObterEscalaSemanaHandler Semana(IUsuarioAtual? u = null) =>
        new(new ObterEscalaSemanaValidator(), _escalas, _feriados, u ?? new UsuarioAtualFalso(Perfis.ProfissionalNap), new RelogioFixo());

    [Fact]
    public async Task Sem_escala_cadastrada_vale_o_padrao()
    {
        var segunda = await Servico().FaixaVigenteAsync(_prof, Segunda, Ct);
        Assert.Equal(new FaixaEscala(DayOfWeek.Monday, new TimeOnly(7, 0), new TimeOnly(17, 0), false), segunda);
        Assert.True((await Servico().FaixaVigenteAsync(_prof, Segunda.AddDays(4), Ct))!.SemAtendimento);
        Assert.Null(await Servico().FaixaVigenteAsync(_prof, Segunda.AddDays(5), Ct));

        await Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda, 7), Ct);
        await Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda.AddDays(4), 10), Ct); // sexta "sem atendimento" não bloqueia
        var ex = await Assert.ThrowsAsync<DomainException>(() => Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda.AddDays(5), 10), Ct));
        Assert.Equal(ForaDaEscala, ex.Message);
    }

    [Fact]
    public async Task Escala_vigente_permite_sabado_quando_configurado()
    {
        await Criar().Handle(new CriarEscalaCommand(_prof, Segunda, null, [Faixa(DayOfWeek.Saturday)]), Ct);
        await Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda.AddDays(5), 8), Ct);
        await Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda.AddDays(5), 11, 45), Ct);
        await Assert.ThrowsAsync<DomainException>(() => Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda.AddDays(5), 12), Ct));
    }

    [Fact]
    public async Task Dia_sem_faixa_e_rejeitado()
    {
        // Escala sem quarta: a quarta deixa de ter expediente para este profissional (e só para ele).
        await Criar().Handle(new CriarEscalaCommand(_prof, Segunda, null, [Faixa(DayOfWeek.Monday), Faixa(DayOfWeek.Tuesday)]), Ct);
        var ex = await Assert.ThrowsAsync<DomainException>(() => Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda.AddDays(2), 9), Ct));
        Assert.Equal(ForaDaEscala, ex.Message);
        await Servico().ExigirDentroDaEscalaAsync(Guid.NewGuid(), Em(Segunda.AddDays(2), 9), Ct);
    }

    [Fact]
    public async Task Escala_nova_vale_so_a_partir_da_vigencia()
    {
        var quarta = Segunda.AddDays(2);
        await Criar().Handle(new CriarEscalaCommand(_prof, quarta, null, [Faixa(DayOfWeek.Monday, 13, 18), Faixa(DayOfWeek.Saturday)]), Ct);

        var semana = await Semana().Handle(new ObterEscalaSemanaQuery(Segunda, _prof), Ct);
        Assert.Equal(Segunda, semana.Inicio);
        Assert.Equal("UTC", semana.Fuso);
        Assert.Equal(7, semana.Dias.Count);
        // Segunda e terça ainda na padrão; de quarta em diante, a nova (sem quarta a sexta, com sábado).
        Assert.Equal((new TimeOnly(7, 0), new TimeOnly(17, 0)), (semana.Dias[0].Inicio!.Value, semana.Dias[0].Fim!.Value));
        Assert.NotNull(semana.Dias[1].Inicio);
        Assert.All(semana.Dias.Skip(2).Take(3), d => Assert.Null(d.Inicio));
        Assert.Equal(new TimeOnly(8, 0), semana.Dias[5].Inicio);
        Assert.Null(semana.Dias[6].Inicio);

        var seguinte = await Semana().Handle(new ObterEscalaSemanaQuery(Segunda.AddDays(7), _prof), Ct);
        Assert.Equal((new TimeOnly(13, 0), new TimeOnly(18, 0)), (seguinte.Dias[0].Inicio!.Value, seguinte.Dias[0].Fim!.Value));
        await Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda, 9), Ct);
        await Assert.ThrowsAsync<DomainException>(() => Servico().ExigirDentroDaEscalaAsync(_prof, Em(Segunda.AddDays(7), 9), Ct));
    }

    [Fact]
    public async Task Semana_da_equipe_usa_o_padrao_e_mostra_feriados()
    {
        _feriados.Adicionar(Feriado.Criar(Segunda.AddDays(1), "Dia do Servidor"));
        await Criar().Handle(new CriarEscalaCommand(_prof, Segunda, null, [Faixa(DayOfWeek.Saturday)]), Ct);

        var semana = await Semana().Handle(new ObterEscalaSemanaQuery(Segunda), Ct);
        Assert.Equal(5, semana.Dias.Count(d => d.Inicio is not null));
        Assert.True(semana.Dias[4].SemAtendimento);
        Assert.Equal("Dia do Servidor", semana.Dias[1].Feriado);
        Assert.Single(semana.Dias, d => d.Feriado is not null);

        await Assert.ThrowsAsync<ValidationException>(() => Semana().Handle(new ObterEscalaSemanaQuery(Segunda.AddDays(1)), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => Semana(new UsuarioAtualFalso(Perfis.Gestao)).Handle(new ObterEscalaSemanaQuery(Segunda), Ct));
    }

    [Fact]
    public async Task Escala_nao_muda_semana_fechada()
    {
        var f = FechamentoSemanal.Criar(_prof, Segunda.AddDays(14));
        f.Fechar(Guid.NewGuid(), DateTimeOffset.UtcNow, Segunda.AddDays(60));
        _fechamentos.Adicionar(f);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Criar().Handle(new CriarEscalaCommand(_prof, Segunda, null, [Faixa(DayOfWeek.Monday)]), Ct));
        Assert.Equal(ServicoFechamento.MensagemPeriodoFechado, ex.Message);

        // Terminar antes da semana fechada passa; encurtar também; prolongar o fim para dentro dela, não.
        var id = await Criar().Handle(new CriarEscalaCommand(_prof, Segunda, Segunda.AddDays(13), [Faixa(DayOfWeek.Monday)]), Ct);
        await Atualizar().Handle(new AtualizarVigenciaEscalaCommand(id, Segunda.AddDays(6), "v"), Ct);
        await Assert.ThrowsAsync<DomainException>(() => Atualizar().Handle(new AtualizarVigenciaEscalaCommand(id, null, "v"), Ct));
    }

    [Fact]
    public async Task Encurtar_vigencia_atravessando_semana_fechada_e_recusado()
    {
        var id = await Criar().Handle(new CriarEscalaCommand(_prof, Segunda, Segunda.AddDays(20), [Faixa(DayOfWeek.Monday)]), Ct);
        var f = FechamentoSemanal.Criar(_prof, Segunda.AddDays(14));
        f.Fechar(Guid.NewGuid(), DateTimeOffset.UtcNow, Segunda.AddDays(60));
        _fechamentos.Adicionar(f);

        // Os dias +7..+20 perderiam a escala, e a semana +14 está fechada.
        var ex = await Assert.ThrowsAsync<DomainException>(() => Atualizar().Handle(new AtualizarVigenciaEscalaCommand(id, Segunda.AddDays(6), "v"), Ct));
        Assert.Equal(ServicoFechamento.MensagemPeriodoFechado, ex.Message);
    }

    [Fact]
    public async Task Vigencias_sobrepostas_retornam_conflito()
    {
        var id = await Criar().Handle(new CriarEscalaCommand(_prof, Segunda, Segunda.AddDays(30), [Faixa(DayOfWeek.Monday)]), Ct);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Criar().Handle(new CriarEscalaCommand(_prof, Segunda.AddDays(30), null, [Faixa(DayOfWeek.Monday)]), Ct));
        Assert.Equal(CriarEscalaHandler.MensagemSobreposicao, ex.Message);
        // Aberta começando antes de uma existente também sobrepõe.
        await Assert.ThrowsAsync<ConflictException>(() =>
            Criar().Handle(new CriarEscalaCommand(_prof, Segunda.AddDays(-10), null, [Faixa(DayOfWeek.Monday)]), Ct));

        // Consecutiva é aceita.
        var seguinte = await Criar().Handle(new CriarEscalaCommand(_prof, Segunda.AddDays(31), null, [Faixa(DayOfWeek.Monday)]), Ct);
        Assert.Equal(2, _escalas.Escalas.Count);

        // Encerrar a seguinte é aceito; reabrir a primeira por cima dela é sobreposição.
        await Atualizar().Handle(new AtualizarVigenciaEscalaCommand(seguinte, Segunda.AddDays(40), "v"), Ct);
        await Assert.ThrowsAsync<ConflictException>(() => Atualizar().Handle(new AtualizarVigenciaEscalaCommand(id, null, "v"), Ct));
        Assert.Contains(("escala.criar", "EscalaProfissional", (Guid?)id), _auditoria.Registros);
        Assert.Contains(("escala.atualizar", "EscalaProfissional", (Guid?)seguinte), _auditoria.Registros);
        Assert.Equal(["v", "v"], _uow.VersoesExigidas);
    }

    [Fact]
    public async Task Escala_exige_administracao_e_profissional_ativo()
    {
        var comando = new CriarEscalaCommand(_prof, Segunda, null, [Faixa(DayOfWeek.Monday)]);
        await Assert.ThrowsAsync<ForbiddenException>(() => Criar(new UsuarioAtualFalso(Perfis.ProfissionalNap)).Handle(comando, Ct));
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Criar().Handle(comando with { ProfissionalId = Guid.NewGuid() }, Ct));
        Assert.Contains("profissionalId", ex.Erros.Keys);
        ex = await Assert.ThrowsAsync<ValidationException>(() => Criar().Handle(comando with { Faixas = [] }, Ct));
        Assert.Contains("faixas", ex.Erros.Keys);
        await Assert.ThrowsAsync<ValidationException>(() => Criar().Handle(comando with { Faixas = [Faixa((DayOfWeek)9)] }, Ct));
        ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Criar().Handle(comando with { Faixas = [Faixa(DayOfWeek.Monday), Faixa(DayOfWeek.Monday, 13, 17)] }, Ct));
        Assert.Contains("faixas", ex.Erros.Keys);
        ex = await Assert.ThrowsAsync<ValidationException>(() => Criar().Handle(comando with
        {
            Faixas = [new FaixaEscalaInput(DayOfWeek.Monday, new TimeOnly(8, 10), new TimeOnly(12, 5), false)],
        }, Ct));
        Assert.Equal(["faixas[0].Inicio", "faixas[0].Fim"], ex.Erros.Keys);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ListarEscalasHandler(_escalas, new UsuarioAtualFalso(Perfis.ProfissionalNap)).Handle(new ListarEscalasQuery(), Ct));
        Assert.Empty(_escalas.Escalas);
    }
}
