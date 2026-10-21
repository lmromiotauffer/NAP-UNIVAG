using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comum;
using Nap.Domain.Producao;

namespace Nap.Application.Features.Producao.Queries;

public record ObterProducaoQuery(TipoPeriodo Tipo, DateOnly Referencia, Guid? ProfissionalId, Guid? CursoId);

public class ObterProducaoValidator : AbstractValidator<ObterProducaoQuery>
{
    public ObterProducaoValidator()
    {
        RuleFor(x => x.Tipo).IsInEnum().WithMessage("Período inválido.");
        RuleFor(x => x.Referencia).Must(d => d.Year is >= 2000 and <= 2100).WithMessage("A data de referência deve estar entre 2000 e 2100.");
    }
}

/// <summary>
/// Produção do período: carrega os fatos de uma vez (período atual e anterior) e calcula por profissional e dia em memória.
/// Com filtro de curso, só as categorias recortáveis por curso (atendimentos, ausências, atividades) e sem tempo/capacidade.
/// </summary>
public sealed class ObterProducaoHandler(IValidator<ObterProducaoQuery> validador, IProducaoQueries consultas, IUsuarioQueries usuarios,
    IUsuarioAtual usuario, IRelogio relogio)
{
    private static readonly CategoriaProducao[] CategoriasDeCurso =
        [CategoriaProducao.AtendimentosRealizados, CategoriaProducao.Ausencias, CategoriaProducao.AtividadesCursos];

    private sealed record DiaCalculado(Guid ProfissionalId, DateOnly Data, ResultadoProducao Resultado);

    public async Task<ProducaoDto> Handle(ObterProducaoQuery consulta, CancellationToken ct = default)
    {
        await validador.ValidarAsync(consulta, ct);

        var hoje = relogio.Hoje;
        var (inicio, fim) = Limites(consulta.Tipo, consulta.Referencia);
        var (inicioAnterior, fimAnterior) = Limites(consulta.Tipo, consulta.Tipo switch
        {
            TipoPeriodo.Semana => inicio.AddDays(-7),
            TipoPeriodo.Mes => inicio.AddMonths(-1),
            TipoPeriodo.Semestre => inicio.AddMonths(-6),
            _ => inicio.AddYears(-1),
        });

        IReadOnlyList<ProfissionalProducaoDto> profissionais;
        Guid? cursoId;
        var porProfissional = true;
        switch (usuario.Perfil)
        {
            case Perfis.ProfissionalNap:
                // Profissional só enxerga a própria produção; o filtro recebido é ignorado.
                profissionais = await Profissionais(inicioAnterior, fim, usuario.Id, ct);
                cursoId = consulta.CursoId;
                porProfissional = false;
                break;
            case Perfis.Administrador or Perfis.CoordenadorNap:
                profissionais = await Profissionais(inicioAnterior, fim, consulta.ProfissionalId, ct);
                cursoId = consulta.CursoId;
                break;
            case Perfis.Gestao:
                // Gestão vê agregados e por profissional; filtro de curso por aluno é ignorado.
                profissionais = await Profissionais(inicioAnterior, fim, consulta.ProfissionalId, ct);
                cursoId = null;
                break;
            case Perfis.CoordenadorCurso:
                cursoId = usuario.CursoId ?? throw new ForbiddenException();
                profissionais = await Profissionais(inicioAnterior, fim, null, ct);
                porProfissional = false;
                break;
            default:
                throw new ForbiddenException();
        }

        var ids = profissionais.Select(p => p.Id).ToList();
        var fatos = await consultas.CarregarFatosAsync(ids, inicioAnterior, fim, cursoId, ct);
        var dias = CalcularDias(fatos, profissionais, inicioAnterior, fim < hoje ? fim : hoje);
        var categorias = cursoId is null ? Enum.GetValues<CategoriaProducao>() : CategoriasDeCurso;

        (IReadOnlyList<CategoriaProducaoDto> Categorias, TempoDto? Tempo, TaxaDto Taxa, int? Pendentes) Agregar(DateOnly de, DateOnly ate,
            Guid? profissionalId = null)
        {
            // ponytail: cada fatia varre todos os dias calculados, O(dias × fatias); agrupar por profissional/mês se o volume crescer.
            bool Inclui(Guid prof, DateOnly data) => data >= de && data <= ate && (profissionalId is null || prof == profissionalId);
            var doRecorte = dias.Where(d => Inclui(d.ProfissionalId, d.Data)).ToList();
            var r = doRecorte.Aggregate(CalculadoraProducao.Calcular([]), (soma, d) => soma.Somar(d.Resultado));
            var ajustes = fatos.Ajustes.Where(a => Inclui(a.ProfissionalId, a.Data)).ToLookup(a => a.Categoria);

            var lista = categorias.Select(c =>
            {
                var (ocorrencias, minutos) = r.Categorias[c];
                var ajusteOcorrencias = c == CategoriaProducao.Ociosidade ? (int?)null : ajustes[c].Sum(a => a.Ocorrencias ?? 0);
                var ajusteMinutos = ajustes[c].Sum(a => a.Minutos ?? 0);
                return new CategoriaProducaoDto(c, c.Rotulo(), new(ocorrencias, minutos), new(ajusteOcorrencias, ajusteMinutos),
                    new(ocorrencias + ajusteOcorrencias, minutos + ajusteMinutos));
            }).ToList();
            var tempo = cursoId is not null ? null : new TempoDto(r.Capacidade, r.ProducaoNaEscala, r.AusenciasNaEscala, r.PendentesNaEscala,
                r.Ociosidade, r.TempoExtra, r.Inconsistente,
                doRecorte.Where(d => d.Resultado.Inconsistente).Select(d => d.Data).Distinct().Order().ToList());
            var (taxa, motivo) = CalculadoraProducao.TaxaAusencia(r.Individuais, r.Faltas);
            return (lista, tempo, new TaxaDto(taxa, r.Faltas, r.Individuais + r.Faltas, motivo),
                cursoId is null ? r.PendentesQuantidade : null);
        }

        var atual = Agregar(inicio, fim);
        var anterior = Agregar(inicioAnterior, fimAnterior).Categorias;
        var comparacao = new ComparacaoDto(new PeriodoProducaoDto(consulta.Tipo, inicioAnterior, fimAnterior, fimAnterior >= hoje),
            atual.Categorias.Zip(anterior, (a, b) =>
            {
                var diferenca = new ValorProducaoDto(a.Total.Ocorrencias - b.Total.Ocorrencias, a.Total.Minutos - b.Total.Minutos);
                return new VariacaoProducaoDto(a.Chave, b.Total, diferenca, Percentual(diferenca.Ocorrencias, b.Total.Ocorrencias),
                    Percentual(diferenca.Minutos, b.Total.Minutos));
            }).ToList());

        var serie = Faixas(consulta.Tipo, inicio, fim)
            .Select(f => new SerieProducaoDto(f.Inicio, f.Fim, f.Fim >= hoje, Agregar(f.Inicio, f.Fim).Categorias))
            .ToList();
        var linhas = !porProfissional ? null : profissionais.Select(p =>
        {
            var a = Agregar(inicio, fim, p.Id);
            return new ProducaoProfissionalLinhaDto(p.Id, p.Nome, a.Categorias, a.Tempo, a.Taxa, a.Pendentes);
        }).ToList();

        return new ProducaoDto(new PeriodoProducaoDto(consulta.Tipo, inicio, fim, fim >= hoje), atual.Categorias, atual.Tempo, atual.Taxa,
            atual.Pendentes, serie, comparacao, linhas);
    }

    /// <summary>Ativos e inativos com fatos na janela; o filtro aceita profissional inativo.</summary>
    private async Task<IReadOnlyList<ProfissionalProducaoDto>> Profissionais(DateOnly inicio, DateOnly fim, Guid? profissionalId,
        CancellationToken ct)
    {
        var lista = await usuarios.ListarProfissionaisDaProducaoAsync(inicio, fim, relogio.Fuso, profissionalId, ct);
        return profissionalId is not null && lista.Count == 0 ? throw new NotFoundException("Profissional") : lista;
    }

    /// <summary>Um resultado por profissional e dia já decorrido; hoje é cortado em "agora".</summary>
    private List<DiaCalculado> CalcularDias(FatosProducao fatos, IReadOnlyList<ProfissionalProducaoDto> profissionais, DateOnly de,
        DateOnly ate)
    {
        var fuso = relogio.Fuso;
        var agora = relogio.Agora;
        var hoje = relogio.Hoje;
        // Minutos inteiros uma única vez, para categorias, escala e tempo extra concordarem.
        static DateTimeOffset Minuto(DateTimeOffset i) => i.AddTicks(-(i.Ticks % TimeSpan.TicksPerMinute));

        // Ocorrências e pendentes entram pela data civil do início; só as já terminadas.
        var ocorrencias = fatos.Ocorrencias.Where(o => o.Fim <= agora)
            .ToLookup(o => (o.ProfissionalId, fuso.DataLocal(o.Inicio)), o => new OcorrenciaProducao(o.Categoria, Minuto(o.Inicio), Minuto(o.Fim), o.Individual));
        var pendentes = fatos.Pendentes
            .ToLookup(p => (p.ProfissionalId, fuso.DataLocal(p.Inicio)), p => (Minuto(p.Inicio), Minuto(p.Fim)));
        var bloqueios = fatos.Bloqueios.ToLookup(b => b.ProfissionalId);
        var escalas = fatos.Escalas.ToLookup(e => e.ProfissionalId);
        var feriados = fatos.Feriados.ToHashSet();

        var dias = new List<DiaCalculado>();
        foreach (var p in profissionais)
        {
            var prof = p.Id;
            // Inativo não tem capacidade a partir da desativação; ocorrências e ajustes continuam contando.
            // ponytail: sem DesativadoEm (desativado antes do campo existir) a capacidade é zero no período inteiro.
            var semCapacidadeDesde = p.Ativo ? DateOnly.MaxValue : p.DesativadoEm ?? DateOnly.MinValue;
            // Nem antes de existir (AtivoDesde nula = criado antes do campo: sem limite).
            var comCapacidadeDesde = p.AtivoDesde ?? DateOnly.MinValue;
            for (var data = de; data <= ate; data = data.AddDays(1))
            {
                // Mesma escolha de EscalaRepository.ObterVigenteAsync: a vigente de início mais recente; sem ela, a padrão.
                var escala = escalas[prof].Where(e => e.Vigente(data)).MaxBy(e => e.VigenciaInicio);
                // Administrador só tem capacidade com escala própria: a padrão vale para a equipe de atendimento.
                var faixa = escala is null && p.Perfil == Perfis.Administrador ? null : ServicoEscala.FaixaDe(escala, data);
                var dia = new DiaProducao(data,
                    faixa is null || data >= semCapacidadeDesde || data < comCapacidadeDesde ? [] : [(fuso.Instante(data, faixa.Inicio), fuso.Instante(data, faixa.Fim))],
                    feriados.Contains(data),
                    bloqueios[prof].Where(b => fuso.DataLocal(b.Inicio) <= data && fuso.DataLocal(b.Fim) >= data).Select(b => (Minuto(b.Inicio), Minuto(b.Fim))).ToList(),
                    ocorrencias[(prof, data)].ToList(), pendentes[(prof, data)].ToList(), data == hoje ? agora : null);
                dias.Add(new DiaCalculado(prof, data, CalculadoraProducao.Calcular([dia])));
            }
        }
        return dias;
    }

    private static (DateOnly Inicio, DateOnly Fim) Limites(TipoPeriodo tipo, DateOnly referencia)
    {
        var mes = new DateOnly(referencia.Year, referencia.Month, 1);
        return tipo switch
        {
            TipoPeriodo.Semana => (ServicoFechamento.SegundaDe(referencia), ServicoFechamento.SegundaDe(referencia).AddDays(6)),
            TipoPeriodo.Mes => (mes, mes.AddMonths(1).AddDays(-1)),
            TipoPeriodo.Semestre => (new DateOnly(referencia.Year, referencia.Month <= 6 ? 1 : 7, 1),
                new DateOnly(referencia.Year, referencia.Month <= 6 ? 6 : 12, 1).AddMonths(1).AddDays(-1)),
            _ => (new DateOnly(referencia.Year, 1, 1), new DateOnly(referencia.Year, 12, 31)),
        };
    }

    /// <summary>Semana: dias; mês: semanas recortadas ao mês; semestre/ano: meses.</summary>
    private static IEnumerable<(DateOnly Inicio, DateOnly Fim)> Faixas(TipoPeriodo tipo, DateOnly inicio, DateOnly fim)
    {
        for (var d = inicio; d <= fim;)
        {
            var ate = tipo switch
            {
                TipoPeriodo.Semana => d,
                TipoPeriodo.Mes => ServicoFechamento.SegundaDe(d).AddDays(6),
                _ => d.AddMonths(1).AddDays(-1),
            };
            if (ate > fim) ate = fim;
            yield return (d, ate);
            d = ate.AddDays(1);
        }
    }

    private static decimal? Percentual(int? diferenca, int? anterior) =>
        diferenca is { } d && anterior is { } a && a != 0 ? (decimal)d / Math.Abs(a) : null;
}
