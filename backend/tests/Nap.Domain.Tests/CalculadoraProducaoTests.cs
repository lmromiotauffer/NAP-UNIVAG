using Nap.Domain.Producao;

namespace Nap.Domain.Tests;

public class CalculadoraProducaoTests
{
    private static readonly DateOnly Segunda = new(2026, 3, 2);

    private static DateTimeOffset H(int hora, int minuto = 0, int dia = 0) =>
        new(2026, 3, 2 + dia, hora, minuto, 0, TimeSpan.Zero);

    private static (DateTimeOffset, DateTimeOffset) I(DateTimeOffset inicio, DateTimeOffset fim) => (inicio, fim);

    private static OcorrenciaProducao Oc(CategoriaProducao c, DateTimeOffset inicio, DateTimeOffset fim, bool individual = false) =>
        new(c, inicio, fim, individual);

    private static DiaProducao Dia(int dia = 0, bool feriado = false, (DateTimeOffset, DateTimeOffset)[]? bloqueios = null,
        OcorrenciaProducao[]? ocorrencias = null, (DateTimeOffset, DateTimeOffset)[]? pendentes = null,
        DateTimeOffset? corte = null, (DateTimeOffset, DateTimeOffset)[]? faixas = null) =>
        new(Segunda.AddDays(dia), faixas ?? [I(H(7, dia: dia), H(17, dia: dia))], feriado, bloqueios ?? [],
            ocorrencias ?? [], pendentes ?? [], corte);

    [Fact]
    public void Capacidade_e_a_faixa_menos_bloqueio_parcial()
    {
        // Bloqueios sobrepostos (9–11 e 10–12) e um que sai da faixa (16–18): desconta 3 h + 1 h, sem contar duas vezes.
        var r = CalculadoraProducao.Calcular([Dia(bloqueios: [I(H(9), H(11)), I(H(10), H(12)), I(H(16), H(18))])]);
        Assert.Equal(600 - 180 - 60, r.Capacidade);
        Assert.Equal(360, r.Ociosidade);
    }

    [Fact]
    public void Feriado_zera_a_capacidade_do_dia()
    {
        var r = CalculadoraProducao.Calcular([Dia(feriado: true), Dia(1)]);
        Assert.Equal(600, r.Capacidade);
    }

    [Fact]
    public void Hoje_conta_ate_agora()
    {
        var r = CalculadoraProducao.Calcular([Dia(corte: H(10, 30))]);
        Assert.Equal(210, r.Capacidade);
    }

    [Fact]
    public void Atendimento_que_passa_do_fechamento_reparte_escala_e_extra()
    {
        var r = CalculadoraProducao.Calcular([Dia(ocorrencias: [Oc(CategoriaProducao.AtendimentosRealizados, H(16, 30), H(17, 30), true)])]);
        Assert.Equal(30, r.ProducaoNaEscala);
        Assert.Equal(30, r.TempoExtra);
        Assert.Equal((1, 60), r.Categorias[CategoriaProducao.AtendimentosRealizados]);
        Assert.Equal(570, r.Ociosidade);
    }

    [Fact]
    public void Atividade_noturna_e_so_tempo_extra()
    {
        var r = CalculadoraProducao.Calcular([Dia(ocorrencias: [Oc(CategoriaProducao.AtividadesCursos, H(19), H(21))])]);
        Assert.Equal(0, r.ProducaoNaEscala);
        Assert.Equal(120, r.TempoExtra);
        Assert.Equal(600, r.Ociosidade);
    }

    [Fact]
    public void Ausencia_fora_da_faixa_nao_gera_extra()
    {
        var r = CalculadoraProducao.Calcular([Dia(ocorrencias: [Oc(CategoriaProducao.Ausencias, H(16, 30), H(17, 30), true)])]);
        Assert.Equal(30, r.AusenciasNaEscala);
        Assert.Equal(0, r.TempoExtra);
        Assert.Equal((1, 30), r.Categorias[CategoriaProducao.Ausencias]);
        Assert.Equal(1, r.Faltas);
    }

    [Fact]
    public void Ociosidade_negativa_marca_inconsistencia()
    {
        // Dois atendimentos sobrepostos ocupando a faixa inteira: 1200 min de produção em 600 de capacidade.
        var r = CalculadoraProducao.Calcular([
            Dia(ocorrencias: [Oc(CategoriaProducao.AtendimentosRealizados, H(7), H(17)), Oc(CategoriaProducao.ReunioesEquipe, H(7), H(17))]),
            Dia(1),
        ]);
        Assert.True(r.Inconsistente);
        Assert.Equal(1200 - 1200, r.Ociosidade);
        Assert.Equal(0, r.Categorias[CategoriaProducao.Ociosidade].Minutos);
        Assert.Null(r.Categorias[CategoriaProducao.Ociosidade].Ocorrencias);

        var so = CalculadoraProducao.Calcular([Dia(ocorrencias: [Oc(CategoriaProducao.AtendimentosRealizados, H(7), H(17)), Oc(CategoriaProducao.ReunioesEquipe, H(7), H(17))])]);
        Assert.Equal(-600, so.Ociosidade);
        Assert.True(so.Inconsistente);
    }

    [Fact]
    public void Identidade_semana_sem_pendencias()
    {
        var dias = new List<DiaProducao>
        {
            Dia(0, ocorrencias: [Oc(CategoriaProducao.AtendimentosRealizados, H(8), H(9), true), Oc(CategoriaProducao.Ausencias, H(16, 30), H(17, 30, 0), true)]),
            Dia(1, bloqueios: [I(H(9, dia: 1), H(10, dia: 1))], ocorrencias: [Oc(CategoriaProducao.ElaboracaoPei, H(13, dia: 1), H(15, dia: 1))]),
            Dia(2, feriado: true, ocorrencias: [Oc(CategoriaProducao.Administrativo, H(9, dia: 2), H(10, dia: 2))]),
            Dia(3, ocorrencias: [Oc(CategoriaProducao.AtividadesCursos, H(19, dia: 3), H(20, dia: 3))]),
            // Troca de escala: sexta com faixa menor (sem atendimento conta como capacidade).
            Dia(4, faixas: [I(H(8, dia: 4), H(12, dia: 4))], ocorrencias: [Oc(CategoriaProducao.ReunioesCoordenacao, H(11, dia: 4), H(13, dia: 4))]),
        };
        var r = CalculadoraProducao.Calcular(dias);

        Assert.Equal(600 + 540 + 0 + 600 + 240, r.Capacidade);
        Assert.Equal(r.Capacidade, r.ProducaoNaEscala + r.AusenciasNaEscala + r.Ociosidade);
        Assert.False(r.Inconsistente);
        Assert.Equal(60 + 120 + 60, r.ProducaoNaEscala);
        Assert.Equal(60 + 60 + 60, r.TempoExtra);

        var somado = dias.Select(d => CalculadoraProducao.Calcular([d])).Aggregate((a, b) => a.Somar(b));
        Assert.Equal(r, somado with { Categorias = r.Categorias });
        Assert.Equal(r.Categorias.OrderBy(c => c.Key), somado.Categorias.OrderBy(c => c.Key));
    }

    [Fact]
    public void Pendentes_contam_so_a_parte_na_escala()
    {
        var r = CalculadoraProducao.Calcular([Dia(pendentes: [I(H(16), H(18)), I(H(19), H(20))])]);
        Assert.Equal(60, r.PendentesNaEscala);
        Assert.Equal(2, r.PendentesQuantidade);
        Assert.Equal(540, r.Ociosidade);
    }

    [Fact]
    public void Ocorrencias_e_minutos_separados()
    {
        var r = CalculadoraProducao.Calcular([Dia(ocorrencias: [
            Oc(CategoriaProducao.AtendimentosRealizados, H(8), H(9), true),
            Oc(CategoriaProducao.AtendimentosRealizados, H(9), H(9, 50), false),
            Oc(CategoriaProducao.ReunioesEquipe, H(10), H(11, 30)),
        ])]);
        Assert.Equal((2, 110), r.Categorias[CategoriaProducao.AtendimentosRealizados]);
        Assert.Equal((1, 90), r.Categorias[CategoriaProducao.ReunioesEquipe]);
        Assert.Equal((0, 0), r.Categorias[CategoriaProducao.ElaboracaoPei]);
        Assert.Equal(8, r.Categorias.Count);
        Assert.Equal(1, r.Individuais);
        Assert.Equal(0, r.Faltas);
    }

    [Fact]
    public void Taxa_sem_denominador_e_nula_com_motivo()
    {
        Assert.Equal((null, "Sem atendimentos individuais no período."), CalculadoraProducao.TaxaAusencia(0, 0));
        Assert.Equal((0.25m, null), CalculadoraProducao.TaxaAusencia(3, 1));
    }

    [Fact]
    public void Rotulos_das_categorias()
    {
        Assert.Equal("Horários disponíveis sem utilização", CategoriaProducao.Ociosidade.Rotulo());
        Assert.Equal("Encaminhamentos, agenda, relatórios e tarefas administrativas", CategoriaProducao.Administrativo.Rotulo());
    }
}
