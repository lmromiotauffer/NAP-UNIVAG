namespace Nap.Domain.Producao;

/// <summary>Ocorrência produtiva ou ausência já concluída. <c>Individual</c> alimenta individuais/faltas (o chamador exclui reuniões).</summary>
public record OcorrenciaProducao(CategoriaProducao Categoria, DateTimeOffset Inicio, DateTimeOffset Fim, bool Individual);

/// <summary>Um dia já decorrido de um profissional, com faixas e bloqueios materializados como instantes absolutos.</summary>
public record DiaProducao(DateOnly Data, IReadOnlyList<(DateTimeOffset Inicio, DateTimeOffset Fim)> Faixas, bool Feriado,
    IReadOnlyList<(DateTimeOffset, DateTimeOffset)> Bloqueios, IReadOnlyList<OcorrenciaProducao> Ocorrencias,
    IReadOnlyList<(DateTimeOffset, DateTimeOffset)> Pendentes, DateTimeOffset? CorteAgora);

public record ResultadoProducao(IReadOnlyDictionary<CategoriaProducao, (int? Ocorrencias, int Minutos)> Categorias,
    int Capacidade, int ProducaoNaEscala, int AusenciasNaEscala, int PendentesNaEscala, int Ociosidade, int TempoExtra,
    int PendentesQuantidade, bool Inconsistente, int Individuais, int Faltas)
{
    public ResultadoProducao Somar(ResultadoProducao outro) => new(
        Categorias.ToDictionary(c => c.Key, c => (c.Value.Ocorrencias + outro.Categorias[c.Key].Ocorrencias,
            c.Value.Minutos + outro.Categorias[c.Key].Minutos)),
        Capacidade + outro.Capacidade, ProducaoNaEscala + outro.ProducaoNaEscala, AusenciasNaEscala + outro.AusenciasNaEscala,
        PendentesNaEscala + outro.PendentesNaEscala, Ociosidade + outro.Ociosidade, TempoExtra + outro.TempoExtra,
        PendentesQuantidade + outro.PendentesQuantidade, Inconsistente || outro.Inconsistente,
        Individuais + outro.Individuais, Faltas + outro.Faltas);
}

public static class CalculadoraProducao
{
    public const string MotivoSemBase = "Sem atendimentos individuais no período.";

    public static ResultadoProducao Calcular(IEnumerable<DiaProducao> dias)
    {
        var resultado = Vazio();
        foreach (var dia in dias)
            resultado = resultado.Somar(CalcularDia(dia));
        return resultado;
    }

    public static (decimal? Taxa, string? Motivo) TaxaAusencia(int individuais, int faltas)
    {
        var base_ = individuais + faltas;
        return base_ == 0 ? (null, MotivoSemBase) : ((decimal)faltas / base_, null);
    }

    private static ResultadoProducao Vazio() => new(
        Enum.GetValues<CategoriaProducao>().ToDictionary(c => c, c => (c == CategoriaProducao.Ociosidade ? (int?)null : 0, 0)),
        0, 0, 0, 0, 0, 0, 0, false, 0, 0);

    // Inconsistência é avaliada por dia: um dia negativo não pode ser escondido por outro com folga.
    private static ResultadoProducao CalcularDia(DiaProducao dia)
    {
        // A faixa do dia já recortada até agora; feriado zera tudo. Bloqueios só descontam capacidade.
        var faixas = dia.Feriado ? [] : Uniao(dia.Faixas);
        if (dia.CorteAgora is { } corte)
            faixas = Menos(faixas, [(corte, DateTimeOffset.MaxValue)]);
        var capacidade = Minutos(Menos(faixas, Uniao(dia.Bloqueios)).Sum(i => (i.Fim - i.Inicio).Ticks));

        var categorias = new Dictionary<CategoriaProducao, (int? Ocorrencias, int Minutos)>(Vazio().Categorias);
        long producao = 0, ausencias = 0, extra = 0;
        int individuais = 0, faltas = 0;
        foreach (var o in dia.Ocorrencias)
        {
            var naEscala = Dentro(o.Inicio, o.Fim, faixas);
            var (ocorrencias, minutos) = categorias[o.Categoria];
            if (o.Categoria == CategoriaProducao.Ausencias)
            {
                ausencias += naEscala;
                categorias[o.Categoria] = (ocorrencias + 1, minutos + Minutos(naEscala));
                if (o.Individual) faltas++;
                continue;
            }
            producao += naEscala;
            extra += (o.Fim - o.Inicio).Ticks - naEscala;
            categorias[o.Categoria] = (ocorrencias + 1, minutos + Minutos((o.Fim - o.Inicio).Ticks));
            if (o.Individual && o.Categoria == CategoriaProducao.AtendimentosRealizados) individuais++;
        }
        var pendentes = Minutos(dia.Pendentes.Sum(p => Dentro(p.Item1, p.Item2, faixas)));

        var ociosidade = capacidade - Minutos(producao) - Minutos(ausencias) - pendentes;
        categorias[CategoriaProducao.Ociosidade] = (null, ociosidade);
        return new ResultadoProducao(categorias, capacidade, Minutos(producao), Minutos(ausencias), pendentes, ociosidade,
            Minutos(extra), dia.Pendentes.Count, ociosidade < 0, individuais, faltas);
    }

    private static int Minutos(long ticks) => (int)(ticks / TimeSpan.TicksPerMinute);

    private static long Dentro(DateTimeOffset inicio, DateTimeOffset fim, List<(DateTimeOffset Inicio, DateTimeOffset Fim)> faixas) =>
        faixas.Sum(f => Math.Max(0, ((fim < f.Fim ? fim : f.Fim) - (inicio > f.Inicio ? inicio : f.Inicio)).Ticks));

    /// <summary>Ordena e funde intervalos sobrepostos ou contíguos.</summary>
    private static List<(DateTimeOffset Inicio, DateTimeOffset Fim)> Uniao(IEnumerable<(DateTimeOffset Inicio, DateTimeOffset Fim)> intervalos)
    {
        var uniao = new List<(DateTimeOffset Inicio, DateTimeOffset Fim)>();
        foreach (var (inicio, fim) in intervalos.Where(i => i.Fim > i.Inicio).OrderBy(i => i.Inicio))
        {
            if (uniao.Count > 0 && inicio <= uniao[^1].Fim)
                uniao[^1] = (uniao[^1].Inicio, fim > uniao[^1].Fim ? fim : uniao[^1].Fim);
            else
                uniao.Add((inicio, fim));
        }
        return uniao;
    }

    /// <summary><paramref name="a"/> menos <paramref name="b"/>; ambos já unidos e ordenados.</summary>
    private static List<(DateTimeOffset Inicio, DateTimeOffset Fim)> Menos(List<(DateTimeOffset Inicio, DateTimeOffset Fim)> a,
        List<(DateTimeOffset Inicio, DateTimeOffset Fim)> b)
    {
        var resto = new List<(DateTimeOffset Inicio, DateTimeOffset Fim)>();
        foreach (var (inicio, fim) in a)
        {
            var atual = inicio;
            foreach (var (bi, bf) in b)
            {
                if (bf <= atual || bi >= fim) continue;
                if (bi > atual) resto.Add((atual, bi));
                atual = bf;
                if (atual >= fim) break;
            }
            if (atual < fim) resto.Add((atual, fim));
        }
        return resto;
    }
}
