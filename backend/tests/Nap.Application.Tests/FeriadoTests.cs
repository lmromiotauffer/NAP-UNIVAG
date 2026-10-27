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

public class FeriadoTests
{
    private static readonly DateOnly Data = new(2026, 10, 28);
    private readonly FeriadoRepositoryEmMemoria _feriados = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new();
    private readonly UsuarioAtualFalso _coord = new(Perfis.CoordenadorNap);
    private readonly FechamentoRepositoryEmMemoria _fechamentos = new();

    private ServicoFechamento Fechamento() => new(_fechamentos, new RelogioFixo());

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private CriarFeriadoHandler Criar(IUsuarioAtual? u = null) =>
        new(new CriarFeriadoValidator(), _feriados, u ?? _coord, _auditoria, _uow, Fechamento());

    [Fact]
    public async Task Feriado_lista_conflitos_sem_alterar_nada()
    {
        var conflito = new ConflitoFeriadoDto(Guid.NewGuid(), "Compromisso", "Atendimento", Guid.NewGuid(), "Ana Beatriz",
            new DateTimeOffset(2026, 10, 28, 13, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 28, 14, 0, 0, TimeSpan.Zero));
        _feriados.Conflitos.Add(conflito);

        var criado = await Criar().Handle(new CriarFeriadoCommand(Data, "  Dia do Servidor "), Ct);

        Assert.Equal([conflito], criado.Conflitos);
        var feriado = Assert.Single(_feriados.Feriados);
        Assert.Equal((criado.Id, Data, "Dia do Servidor", true), (feriado.Id, feriado.Data, feriado.Descricao, feriado.Ativo));
        // Só o feriado e a auditoria: nenhum compromisso ou atendimento é tocado.
        Assert.Equal(1, _uow.Salvamentos);
        Assert.Equal(("feriado.criar", "Feriado", (Guid?)criado.Id), Assert.Single(_auditoria.Registros));
    }

    [Fact]
    public async Task Feriado_em_semana_fechada_e_recusado()
    {
        var f = FechamentoSemanal.Criar(Guid.NewGuid(), ServicoFechamento.SegundaDe(Data));
        f.Fechar(Guid.NewGuid(), DateTimeOffset.UtcNow, Data.AddDays(14));
        _fechamentos.Adicionar(f);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Criar().Handle(new CriarFeriadoCommand(Data, "Dia do Servidor"), Ct));
        Assert.Equal(ServicoFechamento.MensagemPeriodoFechado, ex.Message);
        Assert.Empty(_feriados.Feriados);
        // A semana seguinte segue aberta.
        await Criar().Handle(new CriarFeriadoCommand(Data.AddDays(7), "Dia do Servidor"), Ct);
    }

    [Fact]
    public async Task Feriado_duplicado_retorna_conflito()
    {
        await Criar().Handle(new CriarFeriadoCommand(Data, "Dia do Servidor"), Ct);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => Criar().Handle(new CriarFeriadoCommand(Data, "Outro"), Ct));
        Assert.Equal(CriarFeriadoHandler.MensagemDuplicado, ex.Message);

        // Depois de inativado, a data pode ser cadastrada de novo.
        var id = _feriados.Feriados.Single().Id;
        await new InativarFeriadoHandler(_feriados, _coord, _auditoria, _uow, Fechamento()).Handle(new InativarFeriadoCommand(id), Ct);
        Assert.Contains(("feriado.inativar", "Feriado", (Guid?)id), _auditoria.Registros);
        await Criar().Handle(new CriarFeriadoCommand(Data, "Outro"), Ct);
        Assert.Equal(2, _feriados.Feriados.Count);
    }

    [Fact]
    public async Task Feriado_exige_administracao_e_descricao()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Criar(new UsuarioAtualFalso(Perfis.ProfissionalNap)).Handle(new CriarFeriadoCommand(Data, "Dia do Servidor"), Ct));
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Criar().Handle(new CriarFeriadoCommand(Data, "x"), Ct));
        Assert.Contains("descricao", ex.Erros.Keys);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new InativarFeriadoHandler(_feriados, new UsuarioAtualFalso(Perfis.Sip), _auditoria, _uow, Fechamento()).Handle(new InativarFeriadoCommand(Guid.NewGuid()), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new InativarFeriadoHandler(_feriados, _coord, _auditoria, _uow, Fechamento()).Handle(new InativarFeriadoCommand(Guid.NewGuid()), Ct));
        Assert.Empty(_feriados.Feriados);
    }

    [Fact]
    public async Task Feriados_do_ano_sao_visiveis_a_todos_os_perfis()
    {
        _feriados.Adicionar(Feriado.Criar(new DateOnly(2026, 12, 25), "Natal"));
        _feriados.Adicionar(Feriado.Criar(new DateOnly(2027, 1, 1), "Confraternização"));
        var inativo = Feriado.Criar(new DateOnly(2026, 11, 2), "Finados");
        inativo.Inativar();
        _feriados.Adicionar(inativo);

        var lista = await new ListarFeriadosHandler(new ListarFeriadosValidator(), _feriados).Handle(new ListarFeriadosQuery(2026), Ct);
        Assert.Equal("Natal", Assert.Single(lista).Descricao);
    }
}
