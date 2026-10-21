using Nap.Application.Comum;
using Nap.Application.Features.Peis;
using Nap.Application.Features.Peis.Queries;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Comum;
using Nap.Domain.Peis;

namespace Nap.Application.Tests;

public class PeiConsultasTests
{
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new();
    private readonly RelogioFixo _relogio = new(); // hoje = 2026-10-02

    private sealed class PeiQueriesFalso : IPeiQueries
    {
        public VersaoPeiDetalheDto? Detalhe { get; set; }
        public IReadOnlyList<PeiResumoDto>? Lista { get; set; }
        public EscopoPeis? EscopoRecebido { get; private set; }

        public Task<IReadOnlyList<PeiResumoDto>?> ListarDoAlunoAsync(Guid alunoId, EscopoPeis e, Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(Lista);

        public Task<VersaoPeiDetalheDto?> ObterVersaoAsync(Guid versaoId, EscopoPeis e, IUsuarioAtual u, CancellationToken ct)
        {
            EscopoRecebido = e;
            return Task.FromResult(Detalhe);
        }
    }

    private static VersaoPeiDetalheDto Detalhe(Guid id, EstadoVersaoPei estado = EstadoVersaoPei.Publicada, DateOnly? fim = null) => new(id, Guid.NewGuid(), Guid.NewGuid(), "Maria", "2024001", "Curso", "Ana", 1,
        estado, new ConteudoPeiDto(null, "N", null, null, null, null, new DateOnly(2026, 8, 1), fim ?? new DateOnly(2027, 7, 31)),
        null, [], [], null, null, new AcoesVersaoDto(false, false, false, false, false, false, false, false, false, false), "AAAAAAAAB9E=", false);

    [Fact]
    public async Task Detalhe_audita_a_consulta_e_inexistente_nao_audita()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var consultas = new PeiQueriesFalso { Detalhe = Detalhe(id) };
        var h = new ObterVersaoPeiHandler(new ObterVersaoPeiValidator(), consultas, new UsuarioAtualFalso(Perfis.CoordenadorNap), _auditoria, _uow, _relogio);

        await h.Handle(new ObterVersaoPeiQuery(id), ct);

        var registro = Assert.Single(_auditoria.Registros);
        Assert.Equal("pei.consulta", registro.Acao);
        Assert.Equal(1, _uow.Salvamentos);
        consultas.Detalhe = null;
        await Assert.ThrowsAsync<NotFoundException>(() => h.Handle(new ObterVersaoPeiQuery(id), ct));
        Assert.Single(_auditoria.Registros);
    }

    [Theory]
    [InlineData(Perfis.Sip)]
    [InlineData(Perfis.Gestao)]
    public async Task Sip_e_gestao_recebem_403_antes_de_consultar(string perfil)
    {
        var consultas = new PeiQueriesFalso { Detalhe = Detalhe(Guid.NewGuid()) };
        var h = new ObterVersaoPeiHandler(new ObterVersaoPeiValidator(), consultas, new UsuarioAtualFalso(perfil), _auditoria, _uow, _relogio);
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Handle(new ObterVersaoPeiQuery(Guid.NewGuid()), TestContext.Current.CancellationToken));
        Assert.Null(consultas.EscopoRecebido);
    }

    [Fact]
    public async Task Lista_de_aluno_fora_do_escopo_gera_404()
    {
        var h = new ListarPeisDoAlunoHandler(new ListarPeisDoAlunoValidator(), new PeiQueriesFalso(), new UsuarioAtualFalso(Perfis.CoordenadorNap), _relogio);
        await Assert.ThrowsAsync<NotFoundException>(() => h.Handle(new ListarPeisDoAlunoQuery(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(EstadoVersaoPei.Publicada, "2026-10-01", true)] // fim ontem
    [InlineData(EstadoVersaoPei.Publicada, "2026-10-02", false)] // fim hoje: inclusivo
    [InlineData(EstadoVersaoPei.Rascunho, "2026-10-01", false)]
    [InlineData(EstadoVersaoPei.Arquivada, "2026-10-01", false)]
    public async Task Detalhe_marca_fora_da_vigencia_so_para_publicada_com_fim_passado(EstadoVersaoPei estado, string fim, bool esperado)
    {
        var id = Guid.NewGuid();
        var consultas = new PeiQueriesFalso { Detalhe = Detalhe(id, estado, DateOnly.Parse(fim)) };
        var h = new ObterVersaoPeiHandler(new ObterVersaoPeiValidator(), consultas, new UsuarioAtualFalso(Perfis.CoordenadorNap), _auditoria, _uow, _relogio);

        var dto = await h.Handle(new ObterVersaoPeiQuery(id), TestContext.Current.CancellationToken);

        Assert.Equal(esperado, dto.ForaDaVigencia);
    }

    [Fact]
    public async Task Publicado_com_fim_ontem_esta_fora_da_vigencia()
    {
        var consultas = new PeiQueriesFalso
        {
            Lista = [new PeiResumoDto(Guid.NewGuid(), "Ana", [
                Resumo(EstadoVersaoPei.Publicada, new DateOnly(2026, 10, 1)), Resumo(EstadoVersaoPei.Publicada, new DateOnly(2026, 10, 2)),
                Resumo(EstadoVersaoPei.Rascunho, new DateOnly(2026, 10, 1))])],
        };
        var h = new ListarPeisDoAlunoHandler(new ListarPeisDoAlunoValidator(), consultas, new UsuarioAtualFalso(Perfis.CoordenadorNap), _relogio);

        var versoes = (await h.Handle(new ListarPeisDoAlunoQuery(Guid.NewGuid()), TestContext.Current.CancellationToken))[0].Versoes;

        // Fim_hoje_ainda_esta_vigente e Versao_nao_publicada_nunca_esta_fora_da_vigencia: 2º e 3º itens
        Assert.Equal([true, false, false], versoes.Select(v => v.ForaDaVigencia));
    }

    private static VersaoResumoDto Resumo(EstadoVersaoPei estado, DateOnly fim) =>
        new(Guid.NewGuid(), 1, estado, new DateOnly(2026, 8, 1), fim, null, false, false);
}
