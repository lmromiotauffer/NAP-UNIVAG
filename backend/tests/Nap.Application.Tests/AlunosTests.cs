using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Alunos.Commands;
using Nap.Application.Features.Alunos.Queries;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Alunos;
using Nap.Domain.Comum;
using Nap.Domain.Encaminhamentos;

namespace Nap.Application.Tests;

public class AlunosTests
{
    private readonly Guid _profissional = Guid.NewGuid();
    private readonly Guid _curso = Guid.NewGuid();
    private readonly AlunoRepositoryEmMemoria _repo = new();
    private readonly AlunoQueriesFalso _consultas = new();
    private readonly List<string> _ordem = [];
    private readonly UnidadeDeTrabalhoFalsa _uow;
    private readonly CacheConsultasFalso _cache;
    private readonly AuditoriaFalsa _auditoria = new();

    public AlunosTests()
    {
        _uow = new(_ordem);
        _cache = new(_ordem);
    }

    private CriarAlunoHandler CriarHandler(IUsuarioAtual usuario, params Guid[] ativos) =>
        new(new CriarAlunoValidator(), _repo, new UsuarioQueriesFalso(ativos), new CursoQueriesFalso(_curso), usuario, new RelogioFixo(), _auditoria, _uow, _cache);

    private CriarAlunoHandler CriarHandlerSemCurso() =>
        new(new CriarAlunoValidator(), _repo, new UsuarioQueriesFalso(_profissional), new CursoQueriesFalso(), EquipeNap(), new RelogioFixo(), _auditoria, _uow, _cache);

    private CriarAlunoCommand Comando(string nome = "Maria da Silva", string matricula = "2024001") =>
        new(nome, matricula, _curso, 3, new DateOnly(2026, 8, 1), _profissional, null, null);

    private static UsuarioAtualFalso EquipeNap() => new(Perfis.ProfissionalNap);

    [Fact]
    public async Task Criar_aluno_invalida_o_painel_depois_de_salvar()
    {
        await CriarHandler(EquipeNap(), _profissional).Handle(Comando(), TestContext.Current.CancellationToken);
        Assert.Equal(["salvar", "invalidar"], _ordem);
    }

    [Fact]
    public async Task Criar_aluno_recusa_inicio_ausente_ou_futuro()
    {
        var h = CriarHandler(EquipeNap(), _profissional);
        // RelogioFixo: hoje = 2026-10-02.
        var ausente = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(Comando() with { InicioAcompanhamento = default }, TestContext.Current.CancellationToken));
        var futuro = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(Comando() with { InicioAcompanhamento = new DateOnly(2026, 10, 3) }, TestContext.Current.CancellationToken));

        Assert.Contains("inicioAcompanhamento", ausente.Erros.Keys);
        Assert.Contains("inicioAcompanhamento", futuro.Erros.Keys);
        Assert.Empty(_repo.Alunos);
    }

    [Fact]
    public async Task Criar_aluno_valida_entrada_e_retorna_id()
    {
        var h = CriarHandler(EquipeNap(), _profissional);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(Comando(nome: ""), TestContext.Current.CancellationToken));
        Assert.Contains("nomeCompleto", ex.Erros.Keys);

        var id = await h.Handle(Comando(), TestContext.Current.CancellationToken);
        Assert.Equal(id, Assert.Single(_repo.Alunos).Id);
        Assert.Equal(1, _uow.Salvamentos);
        Assert.Single(_auditoria.Registros);
    }

    [Fact]
    public async Task Matricula_duplicada_gera_conflito()
    {
        var h = CriarHandler(EquipeNap(), _profissional);
        await h.Handle(Comando(), TestContext.Current.CancellationToken);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Handle(Comando(nome: "Outra Pessoa"), TestContext.Current.CancellationToken));
        Assert.Equal("Matrícula já cadastrada", ex.Message);
    }

    [Fact]
    public async Task Coordenador_de_curso_nao_cadastra_aluno()
    {
        var h = CriarHandler(new UsuarioAtualFalso(Perfis.CoordenadorCurso, _curso), _profissional);
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Handle(Comando(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Coordenador_de_curso_consulta_com_escopo_do_proprio_curso_sem_clinico()
    {
        var h = new ListarAlunosHandler(new ListarAlunosValidator(), _consultas, new UsuarioAtualFalso(Perfis.CoordenadorCurso, _curso));
        await h.Handle(new ListarAlunosQuery(), TestContext.Current.CancellationToken);
        Assert.Equal(_curso, _consultas.EscopoRecebido!.SomenteCursoId);
        Assert.False(_consultas.EscopoRecebido.IncluirClinico);
    }

    [Fact]
    public async Task Filtro_de_especialidade_do_encaminhamento_chega_a_consulta()
    {
        var h = new ListarAlunosHandler(new ListarAlunosValidator(), _consultas, EquipeNap());
        await h.Handle(new ListarAlunosQuery(EspecialidadeEncaminhamento: Especialidade.Neuropsicologia), TestContext.Current.CancellationToken);
        Assert.Equal(Especialidade.Neuropsicologia, _consultas.FiltroRecebido!.EspecialidadeEncaminhamento);
    }

    [Fact]
    public async Task Coordenador_de_curso_sem_vinculo_e_negado()
    {
        var h = new ListarAlunosHandler(new ListarAlunosValidator(), _consultas, new UsuarioAtualFalso(Perfis.CoordenadorCurso));
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Handle(new ListarAlunosQuery(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(Perfis.Sip)]
    [InlineData(Perfis.Gestao)]
    public async Task Sip_e_gestao_nao_consultam_alunos(string perfil)
    {
        var h = new ListarAlunosHandler(new ListarAlunosValidator(), _consultas, new UsuarioAtualFalso(perfil));
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Handle(new ListarAlunosQuery(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Consulta_da_ficha_e_auditada()
    {
        var id = Guid.NewGuid();
        _consultas.Ficha = new AlunoFichaDto(id, "Maria", "2024001", _curso, "Direito", 3, _profissional, "Ana", SituacaoAluno.Ativo,
            new DateOnly(2026, 2, 1), null, null, null, "v1");
        var h = new ObterAlunoHandler(new ObterAlunoValidator(), _consultas, EquipeNap(), _auditoria, _uow);

        await h.Handle(new ObterAlunoQuery(id), TestContext.Current.CancellationToken);

        var registro = Assert.Single(_auditoria.Registros);
        Assert.Equal(("aluno.consulta", "Aluno", (Guid?)id), registro);
        Assert.Equal(1, _uow.Salvamentos);
    }

    [Fact]
    public async Task Ficha_fora_do_escopo_nao_grava_consulta()
    {
        var h = new ObterAlunoHandler(new ObterAlunoValidator(), _consultas, EquipeNap(), _auditoria, _uow);

        await Assert.ThrowsAsync<NotFoundException>(() => h.Handle(new ObterAlunoQuery(Guid.NewGuid()), TestContext.Current.CancellationToken));

        Assert.Empty(_auditoria.Registros);
        Assert.Equal(0, _uow.Salvamentos);
    }

    private Guid FichaNoEscopo()
    {
        var id = Guid.NewGuid();
        _consultas.Ficha = new AlunoFichaDto(id, "Maria", "2024001", _curso, "Direito", 3, _profissional, "Ana", SituacaoAluno.Ativo,
            new DateOnly(2026, 2, 1), null, null, null, "v1");
        return id;
    }

    [Theory]
    [InlineData(0)] // no escopo e sem eventos visíveis: 200 vazio, e a consulta é auditada
    [InlineData(2)]
    public async Task Consulta_da_linha_do_tempo_e_auditada(int total)
    {
        var id = FichaNoEscopo();
        _consultas.TotalLinhaDoTempo = total;
        var h = new ListarLinhaDoTempoHandler(new ListarLinhaDoTempoValidator(), _consultas, EquipeNap(), _auditoria, _uow);

        await h.Handle(new ListarLinhaDoTempoQuery(id), TestContext.Current.CancellationToken);

        var registro = Assert.Single(_auditoria.Registros);
        Assert.Equal(("aluno.linha-do-tempo.consulta", "Aluno", (Guid?)id), registro);
        Assert.Equal(1, _uow.Salvamentos);
    }

    [Fact]
    public async Task Linha_do_tempo_no_escopo_sem_eventos_e_auditada()
    {
        var id = FichaNoEscopo();
        var h = new ListarLinhaDoTempoHandler(new ListarLinhaDoTempoValidator(), _consultas, EquipeNap(), _auditoria, _uow);

        var pagina = await h.Handle(new ListarLinhaDoTempoQuery(id), TestContext.Current.CancellationToken);

        Assert.Empty(pagina.Itens);
        Assert.Single(_auditoria.Registros);
    }

    [Fact]
    public async Task Linha_do_tempo_fora_do_escopo_gera_404_e_nao_grava_consulta()
    {
        var h = new ListarLinhaDoTempoHandler(new ListarLinhaDoTempoValidator(), _consultas, EquipeNap(), _auditoria, _uow);

        await Assert.ThrowsAsync<NotFoundException>(() => h.Handle(new ListarLinhaDoTempoQuery(Guid.NewGuid()), TestContext.Current.CancellationToken));

        Assert.Empty(_auditoria.Registros);
        Assert.Equal(0, _uow.Salvamentos);
    }
    [Fact]
    public async Task Ficha_inexistente_ou_fora_do_escopo_gera_not_found()
    {
        var h = new ObterAlunoHandler(new ObterAlunoValidator(), _consultas, EquipeNap(), _auditoria, _uow);
        await Assert.ThrowsAsync<NotFoundException>(() => h.Handle(new ObterAlunoQuery(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Responsavel_precisa_ser_profissional_ativo()
    {
        var h = CriarHandler(EquipeNap());
        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(Comando(), TestContext.Current.CancellationToken));
        Assert.Contains("profissionalResponsavelId", ex.Erros.Keys);
        Assert.Empty(_repo.Alunos);
    }

    [Fact]
    public async Task Listagem_valida_paginacao()
    {
        var h = new ListarAlunosHandler(new ListarAlunosValidator(), _consultas, EquipeNap());
        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(new ListarAlunosQuery(Tamanho: 101), TestContext.Current.CancellationToken));
        Assert.Contains("tamanho", ex.Erros.Keys);
    }

    [Fact]
    public async Task Paginas_acima_do_limite_sao_rejeitadas()
    {
        var lista = new ListarAlunosHandler(new ListarAlunosValidator(), _consultas, EquipeNap());
        var ex = await Assert.ThrowsAsync<ValidationException>(() => lista.Handle(new ListarAlunosQuery(Pagina: 100_001), TestContext.Current.CancellationToken));
        Assert.Contains("pagina", ex.Erros.Keys);
        await lista.Handle(new ListarAlunosQuery(Pagina: 100_000), TestContext.Current.CancellationToken);

        var linha = new ListarLinhaDoTempoHandler(new ListarLinhaDoTempoValidator(), _consultas, EquipeNap(), _auditoria, _uow);
        ex = await Assert.ThrowsAsync<ValidationException>(() => linha.Handle(new ListarLinhaDoTempoQuery(Guid.NewGuid(), Pagina: 100_001), TestContext.Current.CancellationToken));
        Assert.Contains("pagina", ex.Erros.Keys);
    }

    [Fact]
    public async Task Alterar_situacao_audita_e_aluno_inexistente_gera_not_found()
    {
        var id = await CriarHandler(EquipeNap(), _profissional).Handle(Comando(), TestContext.Current.CancellationToken);
        var h = new AlterarSituacaoAlunoHandler(new AlterarSituacaoAlunoValidator(), _repo, EquipeNap(), new RelogioFixo(), _auditoria, _uow, _cache);
        await h.Handle(new AlterarSituacaoAlunoCommand(id, SituacaoAluno.Alta, "Concluiu acompanhamento", "v1"), TestContext.Current.CancellationToken);
        Assert.Contains("v1", _uow.VersoesExigidas);
        Assert.Contains(_auditoria.Registros, r => r.Acao == "aluno.situacao" && r.RecursoId == id);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            h.Handle(new AlterarSituacaoAlunoCommand(Guid.NewGuid(), SituacaoAluno.Alta, "Motivo válido", "v1"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Criar_com_curso_inexistente_gera_erro_no_campo()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => CriarHandlerSemCurso().Handle(Comando(), TestContext.Current.CancellationToken));
        Assert.Contains("cursoId", ex.Erros.Keys);
        Assert.Empty(_repo.Alunos);
    }

    [Fact]
    public async Task Criar_com_matricula_em_formato_invalido_gera_erro_no_campo()
    {
        var h = CriarHandler(EquipeNap(), _profissional);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(Comando(matricula: "ab"), TestContext.Current.CancellationToken));
        Assert.Contains("matricula", ex.Erros.Keys);
    }

    private TransferirCursoAlunoHandler TransferirHandler(params Guid[] cursosAtivos) =>
        new(new TransferirCursoAlunoValidator(), _repo, new CursoQueriesFalso(cursosAtivos), EquipeNap(), new RelogioFixo(), _auditoria, _uow, _cache);

    [Fact]
    public async Task Transferir_para_curso_inexistente_gera_erro_no_campo()
    {
        var id = await CriarHandler(EquipeNap(), _profissional).Handle(Comando(), TestContext.Current.CancellationToken);
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            TransferirHandler().Handle(new TransferirCursoAlunoCommand(id, Guid.NewGuid(), new DateOnly(2026, 9, 1), "Mudou de curso", "v1"), TestContext.Current.CancellationToken));
        Assert.Contains("novoCursoId", ex.Erros.Keys);
    }

    [Fact]
    public async Task Transferir_curso_atualiza_historico()
    {
        var id = await CriarHandler(EquipeNap(), _profissional).Handle(Comando(), TestContext.Current.CancellationToken);
        var novo = Guid.NewGuid();
        await TransferirHandler(novo).Handle(new TransferirCursoAlunoCommand(id, novo, new DateOnly(2026, 9, 1), "Mudou de curso", "v1"), TestContext.Current.CancellationToken);
        var aluno = Assert.Single(_repo.Alunos);
        Assert.Equal(novo, aluno.CursoAtualId);
        Assert.Equal(2, aluno.HistoricoCursos.Count);
        Assert.Contains("v1", _uow.VersoesExigidas);
        Assert.Contains(_auditoria.Registros, r => r.Acao == "aluno.transferir" && r.RecursoId == id);
    }

    private AtualizarAlunoHandler AtualizarHandler() =>
        new(new AtualizarAlunoValidator(), _repo, new UsuarioQueriesFalso(_profissional), EquipeNap(), _auditoria, _uow, _cache);

    [Fact]
    public async Task Atualizar_aluno_sem_mudanca_com_versao_antiga_lanca_conflito_e_nao_salva()
    {
        var id = await CriarHandler(EquipeNap(), _profissional).Handle(Comando(), TestContext.Current.CancellationToken);
        var aluno = Assert.Single(_repo.Alunos);
        var salvamentosAntes = _uow.Salvamentos;
        _uow.VersaoAtual = "v2";
        await Assert.ThrowsAsync<ConflictException>(() => AtualizarHandler().Handle(new AtualizarAlunoCommand(id, aluno.NomeCompleto,
            aluno.Semestre, aluno.NecessidadesEducacionais, aluno.ObservacoesAcademicas, aluno.ProfissionalResponsavelId, "v1"),
            TestContext.Current.CancellationToken));
        Assert.Equal(salvamentosAntes, _uow.Salvamentos);
    }

    [Fact]
    public async Task Atualizar_aluno_altera_dados_e_inexistente_gera_not_found()
    {
        var id = await CriarHandler(EquipeNap(), _profissional).Handle(Comando(), TestContext.Current.CancellationToken);
        await AtualizarHandler().Handle(new AtualizarAlunoCommand(id, "Maria Souza", 5, "Necessidade X", null, _profissional, "v1"), TestContext.Current.CancellationToken);
        var aluno = Assert.Single(_repo.Alunos);
        Assert.Equal("Maria Souza", aluno.NomeCompleto);
        Assert.Equal(5, aluno.Semestre);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            AtualizarHandler().Handle(new AtualizarAlunoCommand(Guid.NewGuid(), "Maria Souza", 5, null, null, _profissional, "v1"), TestContext.Current.CancellationToken));
    }
}
