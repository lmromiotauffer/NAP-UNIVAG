using Nap.Application.Features.Producao;
using Nap.Domain.Producao;

namespace Nap.Infrastructure.Tests;

internal static class ProducaoExportacaoFabrica
{
    public static readonly CabecalhoExportacao Cabecalho = new("01/09/2026 a 30/09/2026", "Mês", "Profissional: Todos · Curso: Todos",
        "Ana Lúcia", new DateTimeOffset(2026, 10, 3, 15, 30, 0, TimeSpan.Zero));

    private static ValorProducaoDto V(int? o, int m) => new(o, m);

    public static IReadOnlyList<CategoriaProducaoDto> Categorias() =>
    [
        new(CategoriaProducao.AtendimentosRealizados, "Atendimentos realizados", V(10, 600), V(2, 125), V(12, 725)),
        new(CategoriaProducao.Ociosidade, "Horários disponíveis sem utilização", V(null, 90), V(null, 0), V(null, 90)),
    ];

    public static ProducaoDto Dto(bool curso = false, bool porProfissional = false, bool parcial = false, bool taxaSemValor = false)
    {
        var periodo = new PeriodoProducaoDto(TipoPeriodo.Mes, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), parcial);
        var tempo = curso ? null : new TempoDto(2400, 725, 120, 60, 90, 30, true, [new DateOnly(2026, 9, 10)]);
        var taxa = taxaSemValor ? new TaxaDto(null, 0, 0, "Sem atendimentos no período.") : new TaxaDto(0.125m, 2, 16, null);
        var serie = new[] { new SerieProducaoDto(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7), false, Categorias()) };
        var anterior = new ComparacaoDto(new PeriodoProducaoDto(TipoPeriodo.Mes, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), false),
        [
            new(CategoriaProducao.AtendimentosRealizados, V(8, 500), V(4, 225), 0.5m, 0.45m),
            new(CategoriaProducao.Ociosidade, V(null, 0), V(null, 90), null, null),
        ]);
        var profissionais = porProfissional
            ? new[] { new ProducaoProfissionalLinhaDto(Guid.NewGuid(), "Bruno Prof", Categorias(), tempo, taxa, 1) }
            : null;
        return new ProducaoDto(periodo, Categorias(), tempo, taxa, 3, serie, anterior, profissionais);
    }
}
