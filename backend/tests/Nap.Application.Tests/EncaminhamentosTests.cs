using Nap.Application.Comum;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Encaminhamentos.Commands;
using Nap.Application.Features.Encaminhamentos.Queries;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Alunos;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Encaminhamentos;

namespace Nap.Application.Tests;

public class EncaminhamentosTests
{
    private readonly Guid _curso = Guid.NewGuid();
    private readonly AlunoRepositoryEmMemoria _alunos = new();
    private readonly EncaminhamentoRepositoryEmMemoria _repo = new();
    private readonly List<string> _ordem = [];
    private readonly UnidadeDeTrabalhoFalsa _uow;
    private readonly CacheConsultasFalso _cache;
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly Aluno _aluno;
    private static readonly DateOnly Hoje = new(2026, 10, 2);

    public EncaminhamentosTests()
    {
        _uow = new(_ordem);
        _cache = new(_ordem);
        _aluno = Aluno.Criar("Maria da Silva", Matricula.Criar("2024001"), _curso, 3, new DateOnly(2026, 8, 1),
            Guid.NewGuid(), null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        _alunos.Alunos.Add(_aluno);
    }

    private static UsuarioAtualFalso Profissional() => new(Perfis.ProfissionalNap);

    private CriarEncaminhamentoHandler CriarHandler(IUsuarioAtual u) =>
        new(new CriarEncaminhamentoValidator(), _repo, _alunos, u, new RelogioFixo(), _auditoria, _uow, _cache);

    private RegistrarRetornoEncaminhamentoHandler RetornoHandler(IUsuarioAtual u) =>
        new(new RegistrarRetornoEncaminhamentoValidator(), _repo, u, new RelogioFixo(), _auditoria, _uow, _cache);

    private CancelarEncaminhamentoHandler CancelarHandler(IUsuarioAtual u) =>
        new(new CancelarEncaminhamentoValidator(), _repo, u, new RelogioFixo(), _auditoria, _uow, _cache);

    private Encaminhamento Semear(DestinoEncaminhamento destino)
    {
        var especialidade = destino == DestinoEncaminhamento.CoordenacaoCurso
            ? Especialidade.CoordenacaoCurso : Especialidade.Psicologia;
        var e = Encaminhamento.Criar(_aluno.Id, _curso, Guid.NewGuid(), especialidade, destino, Hoje.AddDays(-5),
            "motivo restrito", "resumo", null, Hoje, DateTimeOffset.UtcNow);
        _repo.Encaminhamentos.Add(e);
        return e;
    }

    private static RegistrarRetornoEncaminhamentoCommand Acolher(Encaminhamento e, DateOnly? data = null) =>
        new(e.Id, AcaoRetorno.Acolhimento, data ?? Hoje.AddDays(-2), null, "v1");

    private CriarEncaminhamentoCommand Comando(DestinoEncaminhamento destino = DestinoEncaminhamento.Sip) =>
        new(_aluno.Id, Especialidade.Psicologia, destino, Hoje, "motivo restrito", "resumo", null);

    [Fact]
    public async Task Profissional_cria_encaminhamento_com_curso_atual_do_aluno()
    {
        var id = await CriarHandler(Profissional()).Handle(Comando(), TestContext.Current.CancellationToken);

        var e = Assert.Single(_repo.Encaminhamentos);
        Assert.Equal(id, e.Id);
        Assert.Equal(_curso, e.CursoId);
        Assert.Equal(["salvar", "invalidar"], _ordem);
        Assert.Equal("encaminhamento.criar", Assert.Single(_auditoria.Registros).Acao);
    }

    [Fact]
    public async Task Sip_nao_cria_encaminhamento()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CriarHandler(new UsuarioAtualFalso(Perfis.Sip)).Handle(Comando(), TestContext.Current.CancellationToken));
        Assert.Empty(_repo.Encaminhamentos);
    }

    [Fact]
    public async Task Aluno_inexistente_gera_erro_de_campo()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            CriarHandler(Profissional()).Handle(Comando() with { AlunoId = Guid.NewGuid() }, TestContext.Current.CancellationToken));
        Assert.Contains("alunoId", ex.Erros.Keys);
    }

    [Fact]
    public async Task Sip_registra_acolhimento_em_destino_sip()
    {
        var e = Semear(DestinoEncaminhamento.Sip);
        await RetornoHandler(new UsuarioAtualFalso(Perfis.Sip)).Handle(Acolher(e), TestContext.Current.CancellationToken);

        Assert.Equal(EstadoEncaminhamento.Acolhido, e.Estado);
        Assert.Equal(["v1"], _uow.VersoesExigidas);
        Assert.Equal(["salvar", "invalidar"], _ordem);
        Assert.Equal("encaminhamento.retorno", Assert.Single(_auditoria.Registros).Acao);
    }

    [Theory]
    [InlineData(DestinoEncaminhamento.Externo)]
    [InlineData(DestinoEncaminhamento.CoordenacaoCurso)]
    public async Task Sip_nao_ve_encaminhamento_externo_nem_de_coordenacao(DestinoEncaminhamento destino)
    {
        var e = Semear(destino);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            RetornoHandler(new UsuarioAtualFalso(Perfis.Sip)).Handle(Acolher(e), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Profissional_nao_registra_retorno_em_destino_sip()
    {
        var e = Semear(DestinoEncaminhamento.Sip);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            RetornoHandler(Profissional()).Handle(Acolher(e), TestContext.Current.CancellationToken));
        Assert.Equal(0, _uow.Salvamentos);
    }

    [Fact]
    public async Task Equipe_registra_retorno_em_destino_externo()
    {
        var e = Semear(DestinoEncaminhamento.Externo);
        await RetornoHandler(Profissional()).Handle(Acolher(e), TestContext.Current.CancellationToken);
        Assert.Equal(EstadoEncaminhamento.Acolhido, e.Estado);
    }

    [Fact]
    public async Task Coordenador_do_curso_registra_retorno_em_destino_coordenacao()
    {
        var e = Semear(DestinoEncaminhamento.CoordenacaoCurso);
        await RetornoHandler(new UsuarioAtualFalso(Perfis.CoordenadorCurso, _curso))
            .Handle(Acolher(e), TestContext.Current.CancellationToken);
        Assert.Equal(EstadoEncaminhamento.Acolhido, e.Estado);
    }

    [Fact]
    public async Task Coordenador_de_outro_curso_recebe_404()
    {
        var e = Semear(DestinoEncaminhamento.CoordenacaoCurso);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            RetornoHandler(new UsuarioAtualFalso(Perfis.CoordenadorCurso, Guid.NewGuid()))
                .Handle(Acolher(e), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Coordenador_nao_registra_retorno_em_destino_sip_do_proprio_curso()
    {
        var e = Semear(DestinoEncaminhamento.Sip);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            RetornoHandler(new UsuarioAtualFalso(Perfis.CoordenadorCurso, _curso))
                .Handle(Acolher(e), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Retorno_com_versao_antiga_lanca_conflito_e_nao_salva()
    {
        var e = Semear(DestinoEncaminhamento.Externo);
        _uow.VersaoAtual = "v2";
        await Assert.ThrowsAsync<ConflictException>(() =>
            RetornoHandler(Profissional()).Handle(Acolher(e), TestContext.Current.CancellationToken));
        Assert.Equal(0, _uow.Salvamentos);
        Assert.Equal(EstadoEncaminhamento.Encaminhado, e.Estado);
    }

    [Fact]
    public async Task Retorno_com_acao_cancelamento_e_invalido()
    {
        var e = Semear(DestinoEncaminhamento.Externo);
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            RetornoHandler(Profissional()).Handle(Acolher(e) with { Acao = AcaoRetorno.Cancelamento }, TestContext.Current.CancellationToken));
        Assert.Contains("acao", ex.Erros.Keys);
    }

    [Fact]
    public async Task Cancelamento_so_pela_equipe_nap()
    {
        var e = Semear(DestinoEncaminhamento.Sip);
        var cmd = new CancelarEncaminhamentoCommand(e.Id, "Pedido do aluno", "v1");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CancelarHandler(new UsuarioAtualFalso(Perfis.Sip)).Handle(cmd, TestContext.Current.CancellationToken));
        Assert.Equal(0, _uow.Salvamentos);

        await CancelarHandler(Profissional()).Handle(cmd, TestContext.Current.CancellationToken);
        Assert.Equal(EstadoEncaminhamento.Cancelado, e.Estado);
        Assert.Equal("encaminhamento.cancelamento", Assert.Single(_auditoria.Registros).Acao);
        Assert.Equal(["salvar", "invalidar"], _ordem);
    }

    [Fact]
    public async Task Retorno_com_data_fora_de_ordem_lanca_domain_exception_e_nao_salva()
    {
        var e = Semear(DestinoEncaminhamento.Externo);
        await Assert.ThrowsAsync<DomainException>(() =>
            RetornoHandler(Profissional()).Handle(Acolher(e, Hoje.AddDays(-10)), TestContext.Current.CancellationToken));
        Assert.Equal(0, _uow.Salvamentos);
    }

    [Fact]
    public async Task Detalhe_audita_consulta_e_inexistente_gera_not_found_sem_auditar()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var consultas = new EncaminhamentoQueriesFalso
        {
            Detalhe = new EncaminhamentoDetalheDto(id, _aluno.Id, "Maria", "2024001", "Curso", Especialidade.Psicologia,
                DestinoEncaminhamento.Sip, EstadoEncaminhamento.Encaminhado, Hoje, "Ana", false, DateTimeOffset.UtcNow,
                Guid.NewGuid(), "motivo restrito", "resumo", null, null, null, null, true, true, [], "AAAAAAAAB9E="),
        };
        var h = new ObterEncaminhamentoHandler(new ObterEncaminhamentoValidator(), consultas, Profissional(), new RelogioFixo(),
            new ConfiguracaoAlertasFalsa(), _auditoria, _uow);

        await Assert.ThrowsAsync<NotFoundException>(() => h.Handle(new ObterEncaminhamentoQuery(Guid.NewGuid()), ct));
        Assert.Empty(_auditoria.Registros);
        Assert.Equal(0, _uow.Salvamentos);

        Assert.Equal(id, (await h.Handle(new ObterEncaminhamentoQuery(id), ct)).Id);
        Assert.Equal(("encaminhamento.consulta", "Encaminhamento", (Guid?)id), Assert.Single(_auditoria.Registros));
        Assert.Equal(1, _uow.Salvamentos);
    }
}
