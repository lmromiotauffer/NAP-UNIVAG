using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Peis;
using Nap.Application.Features.Producao;
using Nap.Domain.Comum;
using Nap.Domain.Producao;

namespace Nap.Application.Features.Painel.Queries;

public record ObterPainelQuery(DateOnly? Semana = null, Guid? ProfissionalId = null, Guid? CursoId = null);

public class ObterPainelValidator : AbstractValidator<ObterPainelQuery>
{
    public ObterPainelValidator() =>
        RuleFor(x => x.Semana).Must(d => d is null || d.Value.DayOfWeek == DayOfWeek.Monday).WithMessage("A semana deve iniciar na segunda-feira.");
}

/// <summary>Filtros efetivos (já forçados pelo perfil), usados na chave do cache.</summary>
public record FiltrosPainel(DateOnly Semana, Guid? ProfissionalId, Guid? CursoId);

public sealed class ObterPainelHandler(IValidator<ObterPainelQuery> validador, IPainelQueries consultas, ICacheConsultas cache,
    IConfiguracaoAlertas alertas, IUsuarioAtual usuario, IRelogio relogio)
{
    public async Task<PainelDto> Handle(ObterPainelQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);

        var hoje = relogio.Hoje;
        var inicio = query.Semana ?? ServicoFechamento.SegundaDe(hoje);
        var fim = inicio.AddDays(4);
        // Parcial só de segunda a sexta: no fim de semana a semana útil já terminou.
        var periodo = new PeriodoDto(inicio, fim, hoje >= inicio && hoje <= fim);

        var perfil = usuario.Perfil;
        var (profissionalId, cursoId, escopo) = perfil switch
        {
            Perfis.ProfissionalNap => (usuario.Id, query.CursoId, usuario.Id.ToString()),
            Perfis.CoordenadorCurso => (null, usuario.CursoId ?? throw new ForbiddenException(), usuario.CursoId.ToString()),
            Perfis.Sip => (null, null, "todos"),
            Perfis.Administrador or Perfis.CoordenadorNap => (query.ProfissionalId, query.CursoId, "todos"),
            // Gestão só vê agregados da instituição: recortar por curso pequeno ou por profissional expõe a frequência de um aluno.
            Perfis.Gestao => (null, null, "todos"),
            _ => throw new ForbiddenException(),
        };
        var filtros = new FiltrosPainel(inicio, profissionalId, cursoId);

        return await cache.ObterOuCriarAsync($"{perfil}:{escopo}", filtros,
            tct => Montar(perfil, periodo, hoje, filtros, tct), ct);
    }

    private async Task<PainelDto> Montar(string perfil, PeriodoDto periodo, DateOnly hoje, FiltrosPainel f, CancellationToken ct)
    {
        var opcoes = await alertas.ObterDtoAsync(ct);
        var vazio = new PainelDto(periodo, null, null, null, null, null, null, null, null);
        switch (perfil)
        {
            case Perfis.Sip:
                var escopoSip = EscopoEncaminhamentos.Para(usuario);
                return vazio with
                {
                    Encaminhamentos = await consultas.ContarEncaminhamentosAsync(escopoSip, null, hoje, opcoes, ct),
                    Alertas = await consultas.ObterAlertasEncaminhamentoAsync(escopoSip, null, false, hoje, opcoes, ct),
                };
            case Perfis.CoordenadorCurso:
                return vazio with
                {
                    AlunosPorSituacao = await consultas.ContarAlunosPorSituacaoAsync(f.CursoId, ct),
                    Encaminhamentos = await consultas.ContarEncaminhamentosAsync(EscopoEncaminhamentos.Para(usuario), null, hoje, opcoes, ct),
                    Peis = await consultas.ContarPeisAsync(EscopoPeis.Para(usuario), null, null, ct),
                    Atividades = await consultas.ObterAtividadesAsync(hoje, null, f.CursoId, ct),
                };
        }

        var semana = Semana(await consultas.ObterSemanaAsync(periodo.Inicio, periodo.Fim, f.ProfissionalId, f.CursoId, ct));
        var serie = await Serie(hoje, f, ct);
        if (perfil == Perfis.Gestao)
            return vazio with
            {
                Semana = semana,
                Ultimos6Meses = serie,
                PorProfissional = await consultas.ObterPorProfissionalAsync(periodo.Inicio, periodo.Fim, f.CursoId, ct),
                AlunosPorCurso = await consultas.ContarAlunosPorCursoAsync(f.CursoId, ct),
            };

        var escopo = EscopoEncaminhamentos.Para(usuario);
        var listaAlertas = (await consultas.ObterAlertasAsync(hoje, f.ProfissionalId, f.CursoId, opcoes, ct))
            .Concat(await consultas.ObterAlertasEncaminhamentoAsync(escopo, f.ProfissionalId, perfil != Perfis.ProfissionalNap, hoje, opcoes, ct))
            .Concat(await consultas.ObterAlertasPeiAsync(f.ProfissionalId, f.CursoId, perfil != Perfis.ProfissionalNap, hoje, opcoes, ct))
            .ToList();
        var painel = vazio with
        {
            Semana = semana,
            Ultimos6Meses = serie,
            AgendaHoje = await consultas.ObterAgendaDoDiaAsync(hoje, f.ProfissionalId, ct),
            Alertas = listaAlertas,
            Encaminhamentos = await consultas.ContarEncaminhamentosAsync(escopo, f.ProfissionalId, hoje, opcoes, ct),
            Peis = await consultas.ContarPeisAsync(EscopoPeis.Para(usuario), f.ProfissionalId, f.CursoId, ct),
            // Atividades não seguem os filtros de curso/profissional do painel: profissional vê as próprias, coordenação todas.
            Atividades = await consultas.ObterAtividadesAsync(hoje, perfil == Perfis.ProfissionalNap ? f.ProfissionalId : null, null, ct),
        };
        if (perfil == Perfis.ProfissionalNap) return painel;
        return painel with
        {
            AlunosPorCurso = await consultas.ContarAlunosPorCursoAsync(f.CursoId, ct),
            AlunosPorSituacao = await consultas.ContarAlunosPorSituacaoAsync(f.CursoId, ct),
            PorProfissional = await consultas.ObterPorProfissionalAsync(periodo.Inicio, periodo.Fim, f.CursoId, ct),
        };
    }

    // Faltas ficam fora de AtendimentosRealizados/MinutosRealizados; a taxa só existe quando há base.
    private static ProducaoSemanaDto Semana(SemanaBrutaDto b)
    {
        var (taxa, motivo) = CalculadoraProducao.TaxaAusencia(b.Individuais, b.Faltas);
        return new ProducaoSemanaDto(b.Realizados, b.Minutos, b.Individuais, b.Faltas, taxa, motivo, b.CompromissosSemDesfecho);
    }

    // Sempre os 6 meses (do mais antigo ao corrente). A consulta só agrupa meses com registros e cobre o período
    // inteiro: mês sem registro tem de fato 0 realizados e 0 minutos. Só o corrente é parcial; futuro nunca entra.
    private async Task<IReadOnlyList<SerieMensalDto>> Serie(DateOnly hoje, FiltrosPainel f, CancellationToken ct)
    {
        var atual = new DateOnly(hoje.Year, hoje.Month, 1);
        var meses = await consultas.ObterSerieMensalAsync(atual.AddMonths(-5), atual, f.ProfissionalId, f.CursoId, ct);
        return Enumerable.Range(0, 6)
            .Select(i => atual.AddMonths(i - 5))
            .Select(m => (meses.FirstOrDefault(x => x.Ano == m.Year && x.Mes == m.Month) ?? new SerieMensalDto(m.Year, m.Month, 0, 0, false))
                with { Parcial = m == atual })
            .ToList();
    }
}
