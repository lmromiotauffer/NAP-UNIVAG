using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Agenda;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Producao;
// Minuto: minutos inteiros uma única vez, para categorias, escala e tempo extra concordarem.
using static Nap.Domain.Comum.ValueObjects.IntervaloHorario;

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

        var (profissionalId, cursoId) = FiltrosEfetivos(usuario, consulta.ProfissionalId, consulta.CursoId);
        var profissionais = await Profissionais(inicioAnterior, fim, profissionalId, ct);
        // Linhas por profissional só para quem vê a equipe inteira.
        var porProfissional = usuario.Perfil is not (Perfis.ProfissionalNap or Perfis.CoordenadorCurso);

        var ids = profissionais.Select(p => p.Id).ToList();
        var fatos = await consultas.CarregarFatosAsync(ids, inicioAnterior, fim, cursoId, ct);
        var dias = CalcularDias(fatos, profissionais, inicioAnterior, fim < hoje ? fim : hoje);
        var categorias = cursoId is null ? Enum.GetValues<CategoriaProducao>() : CategoriasDeCurso;

        (IReadOnlyList<CategoriaProducaoDto> Categorias, TempoDto? Tempo, TaxaDto Taxa, int? Pendentes) Agregar(DateOnly de, DateOnly ate,
            Guid? profissionalId = null)
        {
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

    /// <summary>
    /// Filtros que o perfil de fato aplica (o recebido pode ser ignorado): Profissional NAP só a própria produção; Gestão sem
    /// recorte de curso (por aluno); coordenador de curso só o seu curso. A exportação usa o mesmo para o cabeçalho.
    /// </summary>
    public static (Guid? ProfissionalId, Guid? CursoId) FiltrosEfetivos(IUsuarioAtual u, Guid? profissionalId, Guid? cursoId) => u.Perfil switch
    {
        Perfis.ProfissionalNap => (u.Id, cursoId),
        Perfis.Administrador or Perfis.CoordenadorNap => (profissionalId, cursoId),
        Perfis.Gestao => (profissionalId, null),
        Perfis.CoordenadorCurso => (null, u.CursoId ?? throw new ForbiddenException()),
        _ => throw new ForbiddenException(),
    };

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

        // Trechos por data civil (no máximo dois: a duração é de até 12 h), fim exclusivo.
        IEnumerable<(DateOnly Data, DateTimeOffset Inicio, DateTimeOffset Fim)> PorDia(DateTimeOffset inicio, DateTimeOffset fim)
        {
            for (var data = fuso.DataLocal(inicio); inicio < fim; data = data.AddDays(1))
            {
                var virada = fuso.InicioDoDia(data.AddDays(1));
                var corte = fim < virada ? fim : virada;
                yield return (data, inicio, corte);
                inicio = corte;
            }
        }

        // Só as já terminadas. A ocorrência conta no dia do início; o trecho que vira o dia entra no dia seguinte e desconta a
        // escala dele, em vez de virar tempo extra do dia do início e ociosidade do seguinte.
        var ocorrencias = fatos.Ocorrencias.Where(o => o.Fim <= agora)
            .SelectMany(o => PorDia(Minuto(o.Inicio), Minuto(o.Fim)).Select((t, i) => (Chave: (o.ProfissionalId, t.Data),
                Ocorrencia: new OcorrenciaProducao(o.Categoria, t.Inicio, t.Fim, o.Individual, Continuacao: i > 0))))
            .ToLookup(x => x.Chave, x => x.Ocorrencia);
        // Pendentes como as ocorrências: o trecho que vira o dia ocupa a escala do seguinte, sem contar de novo.
        var trechosPendentes = fatos.Pendentes
            .SelectMany(p => PorDia(Minuto(p.Inicio), Minuto(p.Fim)).Select((t, i) => (Chave: (p.ProfissionalId, t.Data),
                Trecho: (t.Inicio, t.Fim), Continuacao: i > 0)))
            .ToList();
        var pendentes = trechosPendentes.Where(x => !x.Continuacao).ToLookup(x => x.Chave, x => x.Trecho);
        var pendentesContinuacao = trechosPendentes.Where(x => x.Continuacao).ToLookup(x => x.Chave, x => x.Trecho);
        // Indexados uma vez por (profissional, dia que tocam): o laço de dias não percorre todos os bloqueios a cada dia.
        var bloqueios = fatos.Bloqueios
            .SelectMany(b => PorDia(Minuto(b.Inicio), Minuto(b.Fim)).Select(t => (Chave: (b.ProfissionalId, t.Data),
                Intervalo: (Minuto(b.Inicio), Minuto(b.Fim)))))
            .ToLookup(x => x.Chave, x => x.Intervalo);
        var escalas = fatos.Escalas.ToLookup(e => e.ProfissionalId);
        var feriados = fatos.Feriados.ToHashSet();

        var dias = new List<DiaCalculado>();
        foreach (var p in profissionais)
        {
            var prof = p.Id;
            // Sem capacidade no período de inatividade (InativoEm); ocorrências e ajustes continuam contando.
            // Nem antes de existir (AtivoDesde nula = criado antes do campo: sem limite).
            var comCapacidadeDesde = p.AtivoDesde ?? DateOnly.MinValue;
            for (var data = de; data <= ate; data = data.AddDays(1))
            {
                // Sem escala vigente, a padrão.
                var escala = EscalaProfissional.VigenteEm(escalas[prof], data);
                // Administrador só tem capacidade com escala própria: a padrão vale para a equipe de atendimento.
                var faixa = escala is null && p.Perfil == Perfis.Administrador ? null : ServicoEscala.FaixaDe(escala, data);
                var dia = new DiaProducao(data,
                    faixa is null || p.InativoEm(data) || data < comCapacidadeDesde ? [] : [(fuso.Instante(data, faixa.Inicio), fuso.Instante(data, faixa.Fim))],
                    feriados.Contains(data),
                    bloqueios[(prof, data)].ToList(),
                    ocorrencias[(prof, data)].ToList(), pendentes[(prof, data)].ToList(), data == hoje ? agora : null,
                    pendentesContinuacao[(prof, data)].ToList());
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
