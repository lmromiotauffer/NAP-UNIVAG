using Nap.Application.Comum;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Producao.Commands;
using Nap.Application.Features.Producao.Queries;
using Nap.Application.Features.Usuarios;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Comum;
using Nap.Domain.Producao;

namespace Nap.Application.Tests;

public class AjustesTests
{
    // Relógio fixo: 2026-10-02 (sexta). A semana de 21/09 já terminou.
    private static readonly DateOnly SemanaEncerrada = new(2026, 9, 21);
    private static readonly DateOnly DiaNaSemanaEncerrada = new(2026, 9, 23);

    private sealed class AjustesEmMemoria : IAjusteProducaoRepository
    {
        public List<AjusteProducao> Itens { get; } = [];
        public void Adicionar(AjusteProducao a) => Itens.Add(a);
    }

    private readonly AjustesEmMemoria _ajustes = new();
    private readonly FechamentoRepositoryEmMemoria _fechamentos = new();
    private readonly ProducaoQueriesFalso _consultas = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly CacheConsultasFalso _cache = new();
    private readonly Guid _profissional = Guid.NewGuid();

    private CriarAjusteHandler Criar(IUsuarioAtual usuario, UsuarioQueriesFalso? usuarios = null) =>
        new(new CriarAjusteValidator(), _ajustes, usuarios ?? new UsuarioQueriesFalso(_profissional), usuario, new RelogioFixo(), _auditoria,
            new UnidadeDeTrabalhoFalsa(), _cache, new ServicoFechamento(_fechamentos, new RelogioFixo()));

    private CriarAjusteCommand Comando(DateOnly? data = null, Guid? profissional = null) =>
        new(profissional ?? _profissional, data ?? DiaNaSemanaEncerrada, CategoriaProducao.AtendimentosRealizados, 2, 90, "Atendimentos fora do sistema");

    [Fact]
    public async Task Coordenacao_cria_ajuste_com_auditoria_e_invalida_cache()
    {
        var id = await Criar(new UsuarioAtualFalso(Perfis.CoordenadorNap)).Handle(Comando(), TestContext.Current.CancellationToken);

        Assert.Equal(id, Assert.Single(_ajustes.Itens).Id);
        Assert.Contains(("producao.ajuste", "AjusteProducao", (Guid?)id), _auditoria.Registros);
        Assert.Equal(1, _cache.Invalidacoes);
    }

    [Theory]
    [InlineData(Perfis.ProfissionalNap)]
    [InlineData(Perfis.Gestao)]
    [InlineData(Perfis.CoordenadorCurso)]
    public async Task Fora_da_coordenacao_nao_lanca_ajuste(string perfil)
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Criar(new UsuarioAtualFalso(perfil)).Handle(Comando(), TestContext.Current.CancellationToken));
        Assert.Empty(_ajustes.Itens);
    }

    [Fact]
    public async Task Semana_fechada_rejeita_ajuste()
    {
        var f = FechamentoSemanal.Criar(_profissional, SemanaEncerrada);
        f.Fechar(Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2026, 12, 1));
        _fechamentos.Adicionar(f);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Criar(new UsuarioAtualFalso(Perfis.Administrador)).Handle(Comando(), TestContext.Current.CancellationToken));
        Assert.Equal(ServicoFechamento.MensagemSemanaFechada, ex.Message);
        Assert.Empty(_ajustes.Itens);
    }

    [Fact]
    public async Task Profissional_inexistente_e_erro_no_campo()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Criar(new UsuarioAtualFalso(Perfis.Administrador)).Handle(Comando(profissional: Guid.NewGuid()), TestContext.Current.CancellationToken));
        Assert.Contains("profissionalId", ex.Erros.Keys);
    }

    [Fact]
    public async Task Profissional_inativo_so_aceita_ajuste_antes_da_desativacao()
    {
        var inativo = Guid.NewGuid();
        var usuarios = new UsuarioQueriesFalso();
        usuarios.Inativos.Add(new ProfissionalProducaoDto(inativo, "Antigo", false, new DateOnly(2026, 9, 25)));
        var handler = Criar(new UsuarioAtualFalso(Perfis.CoordenadorNap), usuarios);

        await handler.Handle(Comando(DiaNaSemanaEncerrada, inativo), TestContext.Current.CancellationToken);
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(Comando(new DateOnly(2026, 9, 25), inativo), TestContext.Current.CancellationToken));
        Assert.Contains("profissionalId", ex.Erros.Keys);
    }

    [Fact]
    public async Task Ociosidade_nao_aceita_ajuste()
    {
        await Assert.ThrowsAsync<DomainException>(() => Criar(new UsuarioAtualFalso(Perfis.Administrador))
            .Handle(Comando() with { Categoria = CategoriaProducao.Ociosidade }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Listar_ajustes_e_so_da_coordenacao()
    {
        var handler = new ListarAjustesHandler(new ListarAjustesValidator(), _consultas, new UsuarioAtualFalso(Perfis.ProfissionalNap));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new ListarAjustesQuery(null, SemanaEncerrada, SemanaEncerrada.AddDays(6)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Semanas_da_coordenacao_incluem_inativo_com_fatos()
    {
        var inativo = Guid.NewGuid();
        var usuarios = new UsuarioQueriesFalso(_profissional);
        usuarios.Inativos.Add(new ProfissionalProducaoDto(inativo, "Antigo", false, new DateOnly(2026, 9, 25)));
        var handler = new ListarSemanasHandler(new ListarSemanasValidator(), _consultas, usuarios, new UsuarioAtualFalso(Perfis.CoordenadorNap),
            new RelogioFixo());

        await handler.Handle(new ListarSemanasQuery(null, SemanaEncerrada, SemanaEncerrada.AddDays(6)), TestContext.Current.CancellationToken);

        Assert.Contains(_consultas.ProfissionaisRecebidos!, p => p.Id == inativo);
    }
}
