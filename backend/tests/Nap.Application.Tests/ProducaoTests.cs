using Nap.Application.Comum;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Producao.Queries;
using Nap.Application.Features.Usuarios;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Agenda;
using Nap.Domain.Comum;
using Nap.Domain.Producao;

namespace Nap.Application.Tests;

public class ProducaoTests
{
    // Relógio fixo: sexta 2026-10-02 15:00Z, fuso UTC. Sem escala cadastrada vale a padrão (seg–sex 07–17h = 600 min/dia).
    private readonly ProducaoQueriesFalso _consultas = new();
    private readonly Guid _prof = Guid.NewGuid();
    private readonly Guid _outro = Guid.NewGuid();

    private ObterProducaoHandler Handler(IUsuarioAtual usuario)
    {
        var usuarios = new UsuarioQueriesFalso(_prof, _outro);
        usuarios.Nomes[_prof] = "Ana";
        usuarios.Nomes[_outro] = "Bruno";
        return new ObterProducaoHandler(new ObterProducaoValidator(), _consultas, usuarios, usuario, new RelogioFixo());
    }

    private static DateTimeOffset Em(int ano, int mes, int dia, int hora, int minuto = 0) => new(ano, mes, dia, hora, minuto, 0, TimeSpan.Zero);

    private static OcorrenciaProfissional Atendimento(Guid prof, DateTimeOffset inicio, int minutos = 60,
        CategoriaProducao categoria = CategoriaProducao.AtendimentosRealizados) =>
        new(prof, categoria, inicio, inicio.AddMinutes(minutos), true);

    private void Ocorrencias(params OcorrenciaProfissional[] o) => _consultas.Fatos = _consultas.Fatos with { Ocorrencias = o };

    private Task<ProducaoDto> Obter(IUsuarioAtual usuario, TipoPeriodo tipo, DateOnly referencia, Guid? profissionalId = null,
        Guid? cursoId = null) =>
        Handler(usuario).Handle(new ObterProducaoQuery(tipo, referencia, profissionalId, cursoId), TestContext.Current.CancellationToken);

    private static CategoriaProducaoDto Cat(ProducaoDto p, CategoriaProducao c) => p.Categorias.Single(x => x.Chave == c);

    private UsuarioAtualFalso Coordenacao() => new(Perfis.CoordenadorNap);

    [Fact]
    public async Task Semana_que_cruza_meses_reparte_pela_data()
    {
        Ocorrencias(Atendimento(_prof, Em(2026, 8, 31, 10)), Atendimento(_prof, Em(2026, 9, 1, 10)), Atendimento(_prof, Em(2026, 9, 2, 10)));

        var agosto = await Obter(Coordenacao(), TipoPeriodo.Mes, new DateOnly(2026, 8, 15));
        var setembro = await Obter(Coordenacao(), TipoPeriodo.Mes, new DateOnly(2026, 9, 15));
        var semana = await Obter(Coordenacao(), TipoPeriodo.Semana, new DateOnly(2026, 9, 2));

        Assert.Equal(new ValorProducaoDto(1, 60), Cat(agosto, CategoriaProducao.AtendimentosRealizados).Total);
        Assert.Equal(new ValorProducaoDto(2, 120), Cat(setembro, CategoriaProducao.AtendimentosRealizados).Total);
        Assert.Equal(new ValorProducaoDto(3, 180), Cat(semana, CategoriaProducao.AtendimentosRealizados).Total);
        Assert.Equal((new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 6)), (semana.Periodo.Inicio, semana.Periodo.Fim));
        // A primeira semana de setembro é recortada ao mês: começa no dia 1.
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6)), (setembro.Serie[0].Inicio, setembro.Serie[0].Fim));
        Assert.Equal(2, setembro.Serie[0].Categorias.Single(c => c.Chave == CategoriaProducao.AtendimentosRealizados).Total.Ocorrencias);
        Assert.Equal(new DateOnly(2026, 8, 31), agosto.Serie[^1].Inicio);
        Assert.Equal(7, semana.Serie.Count);
    }

    [Fact]
    public async Task Profissional_ve_so_a_propria()
    {
        var eu = new UsuarioAtualFalso(Perfis.ProfissionalNap) { Id = _prof };
        Ocorrencias(Atendimento(_prof, Em(2026, 9, 22, 10)), Atendimento(_outro, Em(2026, 9, 22, 10)), Atendimento(_outro, Em(2026, 9, 23, 10)));

        var p = await Obter(eu, TipoPeriodo.Semana, new DateOnly(2026, 9, 22), profissionalId: _outro);

        Assert.Equal([_prof], _consultas.IdsRecebidos);
        Assert.Equal(1, Cat(p, CategoriaProducao.AtendimentosRealizados).Total.Ocorrencias);
        Assert.NotNull(p.Tempo);
        Assert.Null(p.PorProfissional);
    }

    [Fact]
    public async Task Coordenador_de_curso_sem_capacidade_e_sem_institucionais()
    {
        var curso = Guid.NewGuid();
        var coordenador = new UsuarioAtualFalso(Perfis.CoordenadorCurso, curso);

        var p = await Obter(coordenador, TipoPeriodo.Mes, new DateOnly(2026, 9, 1), cursoId: Guid.NewGuid());

        Assert.Equal(curso, _consultas.CursoRecebido);
        Assert.Null(p.Tempo);
        Assert.Null(p.Pendentes);
        Assert.Null(p.PorProfissional);
        Assert.Equal([CategoriaProducao.AtendimentosRealizados, CategoriaProducao.Ausencias, CategoriaProducao.AtividadesCursos],
            p.Categorias.Select(c => c.Chave));
        Assert.All(p.Serie, s => Assert.Equal(3, s.Categorias.Count));
        Assert.Equal(3, p.Anterior.Categorias.Count);
    }

    [Fact]
    public async Task Gestao_ve_por_profissional_sem_alunos()
    {
        Ocorrencias(Atendimento(_prof, Em(2026, 9, 22, 10)), Atendimento(_outro, Em(2026, 9, 22, 10)), Atendimento(_outro, Em(2026, 9, 23, 10)));

        var p = await Obter(new UsuarioAtualFalso(Perfis.Gestao), TipoPeriodo.Semana, new DateOnly(2026, 9, 22), cursoId: Guid.NewGuid());

        Assert.Null(_consultas.CursoRecebido); // filtro de curso por aluno não se aplica à gestão
        Assert.NotNull(p.Tempo);
        Assert.NotNull(p.PorProfissional);
        Assert.Equal([("Ana", 1), ("Bruno", 2)], p.PorProfissional
            .Select(l => (l.Profissional, l.Categorias.Single(c => c.Chave == CategoriaProducao.AtendimentosRealizados).Total.Ocorrencias!.Value)));
        Assert.Equal(3, Cat(p, CategoriaProducao.AtendimentosRealizados).Total.Ocorrencias);
    }

    [Fact]
    public async Task Sip_recebe_403()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => Obter(new UsuarioAtualFalso(Perfis.Sip), TipoPeriodo.Semana, new DateOnly(2026, 9, 22)));
        Assert.Null(_consultas.IdsRecebidos);
    }

    [Fact]
    public async Task Ajustes_aparecem_separados_e_fora_da_identidade()
    {
        Ocorrencias(Atendimento(_prof, Em(2026, 9, 21, 10)));
        _consultas.Fatos = _consultas.Fatos with
        {
            Ajustes = [AjusteProducao.Criar(_prof, new DateOnly(2026, 9, 22), CategoriaProducao.ReunioesEquipe, 2, 90, "Reunião esquecida",
                Guid.NewGuid(), Em(2026, 9, 25, 10))],
        };

        var p = await Obter(Coordenacao(), TipoPeriodo.Semana, new DateOnly(2026, 9, 21), profissionalId: _prof);

        var reunioes = Cat(p, CategoriaProducao.ReunioesEquipe);
        Assert.Equal((new ValorProducaoDto(0, 0), new ValorProducaoDto(2, 90), new ValorProducaoDto(2, 90)),
            (reunioes.Registrado, reunioes.Ajustes, reunioes.Total));
        var t = p.Tempo!;
        Assert.Equal((3000, 60, 0, 2940), (t.Capacidade, t.ProducaoNaEscala, t.AusenciasNaEscala, t.Ociosidade));
        Assert.Equal(t.Capacidade, t.ProducaoNaEscala + t.AusenciasNaEscala + t.Ociosidade);
        Assert.Equal(new ValorProducaoDto(null, 2940), Cat(p, CategoriaProducao.Ociosidade).Total);
    }

    [Fact]
    public async Task Comparacao_com_periodo_anterior()
    {
        Ocorrencias(Atendimento(_prof, Em(2026, 9, 14, 10)), Atendimento(_prof, Em(2026, 9, 15, 10)),
            Atendimento(_prof, Em(2026, 9, 21, 10)), Atendimento(_prof, Em(2026, 9, 22, 10)), Atendimento(_prof, Em(2026, 9, 23, 10)));

        var p = await Obter(Coordenacao(), TipoPeriodo.Semana, new DateOnly(2026, 9, 24));

        Assert.Equal(new PeriodoProducaoDto(TipoPeriodo.Semana, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20), false), p.Anterior.Periodo);
        var atendimentos = p.Anterior.Categorias.Single(c => c.Chave == CategoriaProducao.AtendimentosRealizados);
        Assert.Equal((new ValorProducaoDto(2, 120), new ValorProducaoDto(1, 60), 0.5m, 0.5m),
            (atendimentos.Anterior, atendimentos.Diferenca, atendimentos.PercentualOcorrencias, atendimentos.PercentualMinutos));
        var reunioes = p.Anterior.Categorias.Single(c => c.Chave == CategoriaProducao.ReunioesEquipe);
        Assert.Null(reunioes.PercentualOcorrencias);
        Assert.Null(reunioes.PercentualMinutos);
        Assert.Equal((new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 27)), _consultas.JanelaRecebida); // anterior e atual numa só carga
    }

    [Fact]
    public async Task Serie_do_ano_tem_doze_meses()
    {
        Ocorrencias(Atendimento(_prof, Em(2026, 3, 10, 10)));

        var p = await Obter(Coordenacao(), TipoPeriodo.Ano, new DateOnly(2026, 5, 10));

        Assert.Equal(new PeriodoProducaoDto(TipoPeriodo.Ano, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), true), p.Periodo);
        Assert.Equal(12, p.Serie.Count);
        Assert.Equal(Enumerable.Range(1, 12).Select(m => new DateOnly(2026, m, 1)), p.Serie.Select(s => s.Inicio));
        Assert.Equal(new DateOnly(2026, 2, 28), p.Serie[1].Fim);
        Assert.Equal(1, p.Serie[2].Categorias.Single(c => c.Chave == CategoriaProducao.AtendimentosRealizados).Total.Ocorrencias);
        Assert.False(p.Serie[8].Parcial);
        Assert.True(p.Serie[9].Parcial);
        Assert.Equal(new DateOnly(2025, 1, 1), p.Anterior.Periodo.Inicio);
    }

    [Fact]
    public async Task Semestre_vai_de_janeiro_a_junho_e_de_julho_a_dezembro()
    {
        var primeiro = await Obter(Coordenacao(), TipoPeriodo.Semestre, new DateOnly(2026, 6, 30));
        var segundo = await Obter(Coordenacao(), TipoPeriodo.Semestre, new DateOnly(2026, 7, 1));

        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)), (primeiro.Periodo.Inicio, primeiro.Periodo.Fim));
        Assert.Equal(new DateOnly(2025, 7, 1), primeiro.Anterior.Periodo.Inicio);
        Assert.Equal((new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31)), (segundo.Periodo.Inicio, segundo.Periodo.Fim));
        Assert.Equal(new DateOnly(2026, 1, 1), segundo.Anterior.Periodo.Inicio);
    }

    [Fact]
    public async Task Reativado_nao_tem_capacidade_entre_a_desativacao_e_a_reativacao()
    {
        // Desativado na segunda 14/09 e reativado na segunda 21/09: só a semana entre as duas fica sem capacidade (escala padrão: 600 min/dia).
        var volta = Guid.NewGuid();
        var usuarios = new UsuarioQueriesFalso();
        usuarios.Inativos.Add(new ProfissionalProducaoDto(volta, "Volta", true, new DateOnly(2026, 9, 14), ReativadoEm: new DateOnly(2026, 9, 21)));
        var handler = new ObterProducaoHandler(new ObterProducaoValidator(), _consultas, usuarios, Coordenacao(), new RelogioFixo());
        Task<ProducaoDto> Semana(int dia) =>
            handler.Handle(new ObterProducaoQuery(TipoPeriodo.Semana, new DateOnly(2026, 9, dia), volta, null), TestContext.Current.CancellationToken);

        Assert.Equal(3000, (await Semana(9)).Tempo!.Capacidade);
        Assert.Equal(0, (await Semana(16)).Tempo!.Capacidade);
        Assert.Equal(3000, (await Semana(23)).Tempo!.Capacidade);
    }

    [Fact]
    public async Task Capacidade_considera_escala_trocada_no_meio_da_semana()
    {
        // Seg e ter na escala A (08–12h = 240 min); de quarta em diante na B (13–15h = 120 min; sexta "sem atendimento" conta).
        static FaixaEscala[] Faixas(int de, int ate) => Enum.GetValues<DayOfWeek>()
            .Where(d => d is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Select(d => new FaixaEscala(d, new TimeOnly(de, 0), new TimeOnly(ate, 0), d == DayOfWeek.Friday)).ToArray();
        _consultas.Fatos = _consultas.Fatos with
        {
            Escalas =
            [
                EscalaProfissional.Criar(_prof, new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 15), Faixas(8, 12)),
                EscalaProfissional.Criar(_prof, new DateOnly(2026, 9, 16), null, Faixas(13, 15)),
            ],
            // Atendimento às 14h de segunda: fora da faixa A, vira tempo extra; o de quarta às 14h fica na escala B.
            Ocorrencias = [Atendimento(_prof, Em(2026, 9, 14, 14)), Atendimento(_prof, Em(2026, 9, 16, 14))],
        };

        var p = await Obter(Coordenacao(), TipoPeriodo.Semana, new DateOnly(2026, 9, 14), profissionalId: _prof);

        var t = p.Tempo!;
        Assert.Equal((2 * 240 + 3 * 120, 60, 60), (t.Capacidade, t.ProducaoNaEscala, t.TempoExtra));
        Assert.Equal(t.Capacidade - 60, t.Ociosidade);
    }

    [Fact]
    public async Task Atendimento_autorizado_conta_como_tempo_extra()
    {
        // Segunda 2026-09-21, escala padrão 07–17h: o atendimento autorizado às 19h fica fora da faixa.
        Ocorrencias(Atendimento(_prof, Em(2026, 9, 21, 10)), Atendimento(_prof, Em(2026, 9, 21, 19)));

        var p = await Obter(Coordenacao(), TipoPeriodo.Semana, new DateOnly(2026, 9, 21), profissionalId: _prof);

        var t = p.Tempo!;
        Assert.Equal((60, 60), (t.ProducaoNaEscala, t.TempoExtra));
        Assert.False(t.Inconsistente);
        Assert.Equal(t.Capacidade, t.ProducaoNaEscala + t.AusenciasNaEscala + t.Ociosidade);
    }

    [Fact]
    public async Task Ocorrencia_que_vira_o_dia_desconta_a_escala_do_dia_seguinte_sem_contar_duas_vezes()
    {
        // Segunda 20h até terça 8h: a hora 7–8 de terça está na escala, não é tempo extra nem ociosidade.
        Ocorrencias(Atendimento(_prof, Em(2026, 9, 21, 20), minutos: 12 * 60));

        var p = await Obter(Coordenacao(), TipoPeriodo.Semana, new DateOnly(2026, 9, 21), profissionalId: _prof);

        var t = p.Tempo!;
        Assert.Equal((60, 11 * 60), (t.ProducaoNaEscala, t.TempoExtra));
        Assert.Equal(t.Capacidade, t.ProducaoNaEscala + t.AusenciasNaEscala + t.Ociosidade);
        var atendimentos = Cat(p, CategoriaProducao.AtendimentosRealizados).Total;
        Assert.Equal((1, 12 * 60), (atendimentos.Ocorrencias, atendimentos.Minutos));
    }

    [Fact]
    public async Task Pendente_que_vira_o_dia_ocupa_a_escala_do_dia_seguinte_sem_contar_duas_vezes()
    {
        // Segunda 16h30 até terça 7h30: 30 min na escala de segunda e 30 min na de terça.
        _consultas.Fatos = _consultas.Fatos with
        {
            Pendentes = [new IntervaloProfissional(_prof, Em(2026, 9, 21, 16, 30), Em(2026, 9, 22, 7, 30))],
        };

        var p = await Obter(Coordenacao(), TipoPeriodo.Semana, new DateOnly(2026, 9, 21), profissionalId: _prof);

        Assert.Equal(60, p.Tempo!.PendentesNaEscala);
        Assert.Equal(1, p.Pendentes);
    }

    [Fact]
    public async Task Atendimento_autorizado_em_feriado_conta_como_tempo_extra()
    {
        // Terça 2026-09-22 é feriado: sem capacidade, o atendimento autorizado é todo extra.
        _consultas.Fatos = _consultas.Fatos with { Feriados = [new DateOnly(2026, 9, 22)] };
        Ocorrencias(Atendimento(_prof, Em(2026, 9, 22, 10)));

        var p = await Obter(Coordenacao(), TipoPeriodo.Semana, new DateOnly(2026, 9, 21), profissionalId: _prof);

        var t = p.Tempo!;
        Assert.Equal((0, 60), (t.ProducaoNaEscala, t.TempoExtra));
        Assert.False(t.Inconsistente);
        Assert.Equal(t.Capacidade, t.ProducaoNaEscala + t.AusenciasNaEscala + t.Ociosidade);
    }
}
