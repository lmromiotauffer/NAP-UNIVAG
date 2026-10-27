using Nap.Application.Comum;
using Nap.Application.Features.Configuracoes;
using Nap.Application.Features.Painel;
using Nap.Application.Features.Painel.Queries;
using Nap.Application.Features.Peis;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Comum;

namespace Nap.Application.Tests;

public class PainelTests
{
    // RelogioFixo.Hoje = 2026-10-02 (sexta); segunda da semana = 2026-09-28.
    private static readonly DateOnly Segunda = new(2026, 9, 28);
    private readonly PainelQueriesFalso _consultas = new();
    private readonly CacheConsultasFalso _cache = new();
    private readonly ConfiguracaoAlertasDto _opcoes = new(FaltasConsecutivas: 4, FaltasJanela: 5, JanelaDias: 20, DiasSemAcolhimento: 7,
        DiasSemAtualizacao: 15, DiasPeiPendente: 15, Versao: "v1");

    private ObterPainelHandler Criar(IUsuarioAtual usuario) =>
        new(new ObterPainelValidator(), _consultas, _cache, new ConfiguracaoAlertasFalsa(_opcoes), usuario, new RelogioFixo());

    private Task<PainelDto> Obter(IUsuarioAtual usuario, ObterPainelQuery? query = null) =>
        Criar(usuario).Handle(query ?? new ObterPainelQuery(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Taxa_de_ausencia_sem_base_e_nula_com_motivo()
    {
        _consultas.Semana = new(0, 0, 0, 0, 2);
        var dto = await Obter(new UsuarioAtualFalso(Perfis.CoordenadorNap));
        Assert.Null(dto.Semana!.TaxaAusencia);
        Assert.Equal("Sem atendimentos individuais no período.", dto.Semana.MotivoNaoCalculado);
        Assert.Equal(2, dto.Semana.CompromissosSemDesfecho);
    }

    [Fact]
    public async Task Taxa_de_ausencia_com_base_e_calculada_sem_motivo()
    {
        _consultas.Semana = new(4, 200, 3, 1, 0);
        var s = (await Obter(new UsuarioAtualFalso(Perfis.CoordenadorNap))).Semana!;
        Assert.Equal(0.25m, s.TaxaAusencia);
        Assert.Null(s.MotivoNaoCalculado);
    }

    [Fact]
    public async Task Ausencias_nao_entram_em_realizados()
    {
        _consultas.Semana = new(4, 200, 3, 2, 0);
        var s = (await Obter(new UsuarioAtualFalso(Perfis.CoordenadorNap))).Semana!;
        Assert.Equal(4, s.AtendimentosRealizados);
        Assert.Equal(200, s.MinutosRealizados);
        Assert.Equal(2, s.Faltas);
    }

    [Fact]
    public async Task Profissional_ve_somente_os_proprios_dados_mesmo_filtrando_outro()
    {
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var dto = await Obter(usuario, new ObterPainelQuery(null, Guid.NewGuid(), null));
        Assert.Equal(usuario.Id, _consultas.SemanaRecebida!.Value.ProfissionalId);
        Assert.Equal(usuario.Id, _consultas.ProfissionalDaAgenda);
        Assert.Equal(usuario.Id, _consultas.ProfissionalDaSerie);
        Assert.Equal(usuario.Id, _consultas.ProfissionalDosAlertas);
        Assert.NotNull(dto.Semana);
        Assert.NotNull(dto.AgendaHoje);
        Assert.NotNull(dto.Alertas);
        Assert.NotNull(dto.Ultimos6Meses);
        Assert.Null(dto.PorProfissional);
        Assert.Null(dto.AlunosPorCurso);
        Assert.Null(dto.AlunosPorSituacao);
        Assert.Equal(usuario.Id, ((FiltrosPainel)_cache.Consultas[0].Filtros).ProfissionalId);
    }

    [Fact]
    public async Task Gestao_nao_recebe_alunos_identificados()
    {
        var dto = await Obter(new UsuarioAtualFalso(Perfis.Gestao));
        Assert.Null(dto.Alertas);
        Assert.Null(dto.AgendaHoje);
        Assert.Null(dto.AlunosPorSituacao);
        Assert.NotNull(dto.Semana);
        Assert.NotNull(dto.Ultimos6Meses);
        Assert.NotNull(dto.PorProfissional);
        Assert.NotNull(dto.AlunosPorCurso);
        Assert.DoesNotContain(nameof(IPainelQueries.ObterAlertasAsync), _consultas.Chamadas);
        Assert.DoesNotContain(nameof(IPainelQueries.ObterAgendaDoDiaAsync), _consultas.Chamadas);
    }

    [Fact]
    public async Task Coordenador_de_curso_recebe_curso_forcado()
    {
        var curso = Guid.NewGuid();
        var dto = await Obter(new UsuarioAtualFalso(Perfis.CoordenadorCurso, curso), new ObterPainelQuery(null, null, Guid.NewGuid()));
        Assert.Equal(curso, _consultas.CursoDaSituacao);
        Assert.NotNull(dto.AlunosPorSituacao);
        Assert.Null(dto.Semana);
        Assert.Null(dto.AgendaHoje);
        Assert.Null(dto.Alertas);
        Assert.Null(dto.Ultimos6Meses);
        Assert.Null(dto.AlunosPorCurso);
        Assert.Null(dto.PorProfissional);
        Assert.Equal(curso, ((FiltrosPainel)_cache.Consultas[0].Filtros).CursoId);
    }

    [Fact]
    public async Task Coordenador_de_curso_sem_vinculo_e_negado()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => Obter(new UsuarioAtualFalso(Perfis.CoordenadorCurso)));
    }

    [Fact]
    public async Task Painel_do_sip_conta_aguardando_em_atendimento_e_sem_atualizacao()
    {
        _consultas.Encaminhamentos = new(5, 2, 1, 3, 1);
        var dto = await Obter(new UsuarioAtualFalso(Perfis.Sip), new ObterPainelQuery(null, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(new EncaminhamentosPainelDto(5, 2, 1, 3, 1, 15), dto.Encaminhamentos);
        Assert.True(_consultas.ContagemRecebida!.Value.Escopo.SoDestinosSip);
        Assert.Null(_consultas.ContagemRecebida.Value.SolicitanteId);
        Assert.Null(dto.Semana);
        Assert.Null(dto.AgendaHoje);
        Assert.Null(dto.Ultimos6Meses);
        Assert.Null(dto.AlunosPorCurso);
        Assert.Null(dto.AlunosPorSituacao);
        Assert.Null(dto.PorProfissional);
        Assert.Equal([nameof(IPainelQueries.ContarEncaminhamentosAsync), nameof(IPainelQueries.ObterAlertasEncaminhamentoAsync)], _consultas.Chamadas);
        Assert.False(_consultas.AlertasEncaminhamentoRecebidos!.Value.SemDestinatario);
        Assert.Equal("Sip:todos", _cache.Consultas[0].Escopo);
        var filtros = (FiltrosPainel)_cache.Consultas[0].Filtros;
        Assert.Null(filtros.ProfissionalId);
        Assert.Null(filtros.CursoId);
    }

    [Fact]
    public async Task Painel_do_profissional_conta_so_os_que_ele_solicitou()
    {
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var dto = await Obter(usuario, new ObterPainelQuery(null, Guid.NewGuid(), null));
        Assert.NotNull(dto.Encaminhamentos);
        Assert.Equal(usuario.Id, _consultas.ContagemRecebida!.Value.SolicitanteId);
        Assert.Equal(usuario.Id, _consultas.AlertasEncaminhamentoRecebidos!.Value.SolicitanteId);
    }

    [Fact]
    public async Task Alerta_sem_acolhimento_aparece_apos_sete_dias()
    {
        _consultas.AlertasEncaminhamento = [new("encaminhamento-sem-acolhimento", "Encaminhamento sem acolhimento", "Aluno: encaminhado há 8 dias sem acolhimento", Guid.NewGuid(), 8)];
        var dto = await Obter(new UsuarioAtualFalso(Perfis.Sip));
        var alerta = Assert.Single(dto.Alertas!);
        Assert.Equal(("encaminhamento-sem-acolhimento", 8), (alerta.Tipo, alerta.DiasOuOcorrencias));
    }

    [Theory]
    [InlineData(Perfis.Administrador, true)]
    [InlineData(Perfis.CoordenadorNap, true)]
    [InlineData(Perfis.ProfissionalNap, false)]
    [InlineData(Perfis.Sip, false)]
    public async Task Alerta_sem_destinatario_so_para_coordenacao(string perfil, bool esperado)
    {
        await Obter(new UsuarioAtualFalso(perfil));
        Assert.Equal(esperado, _consultas.AlertasEncaminhamentoRecebidos!.Value.SemDestinatario);
    }

    [Fact]
    public async Task Painel_do_coordenador_conta_so_o_proprio_curso()
    {
        var curso = Guid.NewGuid();
        var dto = await Obter(new UsuarioAtualFalso(Perfis.CoordenadorCurso, curso));
        Assert.NotNull(dto.Encaminhamentos);
        Assert.Equal(curso, _consultas.ContagemRecebida!.Value.Escopo.SomenteCursoId);
        Assert.Null(_consultas.ContagemRecebida.Value.SolicitanteId);
    }

    [Fact]
    public async Task Gestao_nao_recebe_encaminhamentos()
    {
        var dto = await Obter(new UsuarioAtualFalso(Perfis.Gestao));
        Assert.Null(dto.Encaminhamentos);
        Assert.DoesNotContain(nameof(IPainelQueries.ContarEncaminhamentosAsync), _consultas.Chamadas);
    }

    [Fact]
    public async Task Coordenacao_recebe_todos_os_blocos_com_filtros()
    {
        var prof = Guid.NewGuid();
        var curso = Guid.NewGuid();
        var dto = await Obter(new UsuarioAtualFalso(Perfis.Administrador), new ObterPainelQuery(null, prof, curso));
        Assert.NotNull(dto.Semana);
        Assert.NotNull(dto.AgendaHoje);
        Assert.NotNull(dto.Ultimos6Meses);
        Assert.NotNull(dto.AlunosPorCurso);
        Assert.NotNull(dto.AlunosPorSituacao);
        Assert.NotNull(dto.Alertas);
        Assert.NotNull(dto.PorProfissional);
        Assert.NotNull(dto.Encaminhamentos);
        Assert.Equal(prof, _consultas.ContagemRecebida!.Value.SolicitanteId);
        Assert.Equal(prof, _consultas.SemanaRecebida!.Value.ProfissionalId);
        Assert.Equal(curso, _consultas.SemanaRecebida!.Value.CursoId);
        // Encaminhamentos também seguem o filtro de curso, sem perder motivo e observações da equipe.
        var escopo = _consultas.ContagemRecebida!.Value.Escopo;
        Assert.Equal((false, curso, true, true), (escopo.Todos, escopo.SomenteCursoId, escopo.VerMotivo, escopo.VerObservacoes));
        Assert.Equal(escopo, _consultas.AlertasEncaminhamentoRecebidos!.Value.Escopo);
    }

    [Fact]
    public async Task Cache_recebe_escopo_do_usuario()
    {
        var prof = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        await Obter(prof);
        var curso = Guid.NewGuid();
        await Obter(new UsuarioAtualFalso(Perfis.CoordenadorCurso, curso));
        await Obter(new UsuarioAtualFalso(Perfis.Gestao));
        Assert.Equal($"ProfissionalNap:{prof.Id}", _cache.Consultas[0].Escopo);
        Assert.Equal($"CoordenadorCurso:{curso}", _cache.Consultas[1].Escopo);
        Assert.Equal("Gestao:todos", _cache.Consultas[2].Escopo);
    }

    [Fact]
    public async Task Periodo_padrao_e_a_segunda_da_semana_corrente_parcial_com_fim_no_domingo()
    {
        var dto = await Obter(new UsuarioAtualFalso(Perfis.CoordenadorNap));
        Assert.Equal(new PeriodoDto(Segunda, Segunda.AddDays(6), true), dto.Periodo);
        Assert.Equal((Segunda, Segunda.AddDays(6)), (_consultas.SemanaRecebida!.Value.Inicio, _consultas.SemanaRecebida!.Value.Fim));
    }

    [Fact]
    public async Task No_sabado_a_semana_corrente_ainda_e_parcial()
    {
        var sabado = new RelogioFixo { Agora = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero) };
        var dto = await new ObterPainelHandler(new ObterPainelValidator(), _consultas, _cache, new ConfiguracaoAlertasFalsa(_opcoes),
            new UsuarioAtualFalso(Perfis.CoordenadorNap), sabado).Handle(new ObterPainelQuery(), TestContext.Current.CancellationToken);
        Assert.Equal(new PeriodoDto(Segunda, Segunda.AddDays(6), true), dto.Periodo);
    }

    [Fact]
    public async Task Semana_passada_nao_e_parcial()
    {
        var dto = await Obter(new UsuarioAtualFalso(Perfis.CoordenadorNap), new ObterPainelQuery(Segunda.AddDays(-7)));
        Assert.False(dto.Periodo.Parcial);
    }

    [Fact]
    public async Task Semana_que_nao_e_segunda_gera_erro_de_validacao()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Obter(new UsuarioAtualFalso(Perfis.CoordenadorNap), new ObterPainelQuery(Segunda.AddDays(1))));
        Assert.Contains("semana", ex.Erros.Keys);
    }

    [Fact]
    public async Task Serie_mensal_tem_seis_meses_em_ordem_com_lacuna_zerada_parcial_no_corrente_e_sem_futuro()
    {
        _consultas.Serie =
        [
            new(2026, 5, 3, 150, false), new(2026, 9, 4, 200, false), new(2026, 10, 1, null, false), new(2026, 11, 9, 9, false),
        ];
        var dto = await Obter(new UsuarioAtualFalso(Perfis.CoordenadorNap));
        Assert.Equal((new DateOnly(2026, 5, 1), new DateOnly(2026, 10, 1)), _consultas.SerieRecebida);
        var serie = dto.Ultimos6Meses!;
        Assert.Equal([5, 6, 7, 8, 9, 10], serie.Select(m => m.Mes));
        Assert.All(serie, m => Assert.Equal(2026, m.Ano));
        Assert.DoesNotContain(serie, m => m.Mes == 11);
        var lacuna = serie.Single(m => m.Mes == 7);
        Assert.Equal((0, 0, false), (lacuna.Realizados, lacuna.Minutos, lacuna.Parcial));
        Assert.Equal((3, 150), (serie[0].Realizados, serie[0].Minutos));
        var atual = serie[^1];
        Assert.Equal(10, atual.Mes);
        Assert.True(atual.Parcial);
        Assert.Null(atual.Minutos);
        Assert.Single(serie, m => m.Parcial);
    }

    [Fact]
    public async Task Limiares_dos_alertas_vem_das_opcoes()
    {
        await Obter(new UsuarioAtualFalso(Perfis.CoordenadorNap));
        Assert.Same(_opcoes, _consultas.OpcoesRecebidas);
    }

    [Fact]
    public async Task Painel_do_profissional_conta_so_os_peis_dele_e_alerta_sem_coordenador_fica_de_fora()
    {
        _consultas.Peis = new(2, 1, 0);
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        var curso = Guid.NewGuid();
        var dto = await Obter(usuario, new ObterPainelQuery(null, Guid.NewGuid(), curso));
        Assert.Equal(new PeisPainelDto(2, 1, 0), dto.Peis);
        Assert.Equal(curso, _consultas.PeisRecebidos!.Value.CursoId);
        Assert.Equal(usuario.Id, _consultas.PeisRecebidos!.Value.ResponsavelId);
        Assert.Equal((usuario.Id, (Guid?)curso, false), _consultas.AlertasPeiRecebidos!.Value);
    }

    [Theory]
    [InlineData(Perfis.Administrador)]
    [InlineData(Perfis.CoordenadorNap)]
    public async Task Painel_da_coordenacao_conta_todos_os_peis_e_inclui_sem_coordenador(string perfil)
    {
        var dto = await Obter(new UsuarioAtualFalso(perfil));
        Assert.NotNull(dto.Peis);
        Assert.True(_consultas.PeisRecebidos!.Value.Escopo.Todos);
        Assert.Null(_consultas.PeisRecebidos.Value.ResponsavelId);
        Assert.Equal((null, null, true), _consultas.AlertasPeiRecebidos!.Value);
    }

    [Fact]
    public async Task Painel_do_coordenador_de_curso_conta_peis_do_curso_sem_alertas_de_pei()
    {
        var curso = Guid.NewGuid();
        _consultas.Peis = new(0, 0, 3);
        var dto = await Obter(new UsuarioAtualFalso(Perfis.CoordenadorCurso, curso));
        Assert.Equal(new PeisPainelDto(0, 0, 3), dto.Peis);
        Assert.Equal(curso, _consultas.PeisRecebidos!.Value.Escopo.SomenteCursoId);
        Assert.Null(_consultas.PeisRecebidos.Value.ResponsavelId);
        Assert.DoesNotContain(nameof(IPainelQueries.ObterAlertasPeiAsync), _consultas.Chamadas);
        // O cache é por curso (não por usuário): a contagem não depende de quem consulta.
        Assert.Equal($"CoordenadorCurso:{curso}", _cache.Consultas[0].Escopo);
    }

    [Theory]
    [InlineData(Perfis.Sip)]
    [InlineData(Perfis.Gestao)]
    public async Task Sip_e_gestao_nao_recebem_peis(string perfil)
    {
        var dto = await Obter(new UsuarioAtualFalso(perfil));
        Assert.Null(dto.Peis);
        Assert.DoesNotContain(nameof(IPainelQueries.ContarPeisAsync), _consultas.Chamadas);
        Assert.DoesNotContain(nameof(IPainelQueries.ObterAlertasPeiAsync), _consultas.Chamadas);
    }

    [Fact]
    public async Task Painel_do_profissional_conta_atividades_do_mes()
    {
        var usuario = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        _consultas.Atividades = new(2, 150, []);
        var dto = await Obter(usuario, new ObterPainelQuery(null, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(new AtividadesPainelDto(2, 150, []), dto.Atividades);
        Assert.Equal((usuario.Id, (Guid?)null), _consultas.AtividadesRecebidas);
    }

    [Fact]
    public async Task Painel_da_coordenacao_conta_todas_as_atividades()
    {
        await Obter(new UsuarioAtualFalso(Perfis.CoordenadorNap), new ObterPainelQuery(null, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(((Guid?)null, (Guid?)null), _consultas.AtividadesRecebidas);
    }

    [Fact]
    public async Task Painel_do_coordenador_de_curso_mostra_as_do_curso()
    {
        var curso = Guid.NewGuid();
        await Obter(new UsuarioAtualFalso(Perfis.CoordenadorCurso, curso));
        Assert.Equal(((Guid?)null, (Guid?)curso), _consultas.AtividadesRecebidas);
    }

    [Theory]
    [InlineData(Perfis.Sip)]
    [InlineData(Perfis.Gestao)]
    public async Task Sip_e_gestao_nao_recebem_atividades(string perfil)
    {
        var dto = await Obter(new UsuarioAtualFalso(perfil));
        Assert.Null(dto.Atividades);
        Assert.DoesNotContain(nameof(IPainelQueries.ObterAtividadesAsync), _consultas.Chamadas);
    }
}
