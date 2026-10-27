using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Configuracoes;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Painel;
using Nap.Application.Features.Peis;
using Nap.Domain.Agenda;
using Nap.Domain.Alunos;
using Nap.Domain.Atendimentos;
using Nap.Domain.Atividades;
using Nap.Domain.Comum;
using Nap.Domain.Encaminhamentos;
using Nap.Domain.Peis;

namespace Nap.Infrastructure.Persistencia.Consultas;

/// <summary>Números brutos do painel. Faltas contam só atendimentos individuais (reuniões ficam fora da base da taxa).</summary>
public sealed class PainelQueries(NapDbContext db, IRelogio relogio) : IPainelQueries
{
    /// <summary>Teto de alertas por tipo no painel; acima dele, um alerta-resumo diz quantos ficaram de fora.</summary>
    internal const int LimitePorTipo = 50;

    /// <summary>Teto de <c>IntervaloHorario</c>: recuar o início da janela por ele alcança o que começou antes e entra nela.</summary>
    internal const int DuracaoMaximaHoras = 12;

    private static List<AlertaDto> Limitados(IEnumerable<AlertaDto> primeiros, int total, string tipo, string titulo)
    {
        var alertas = primeiros.ToList();
        if (total > alertas.Count)
            alertas.Add(new AlertaDto(tipo, titulo, $"E mais {total - alertas.Count} não listados aqui", null, total - alertas.Count));
        return alertas;
    }

    public async Task<SemanaBrutaDto> ObterSemanaAsync(DateOnly inicio, DateOnly fim, Guid? profissionalId, Guid? cursoId, CancellationToken ct)
    {
        var de = relogio.Fuso.InicioDoDia(inicio);
        var ate = relogio.Fuso.InicioDoDia(fim.AddDays(1));
        // Mesma regra da produção: conta no dia do início, mas os minutos ficam com o dia (e a semana) em que caem.
        var bruto = await Atendimentos(db, relogio, de.AddHours(-DuracaoMaximaHoras), ate, profissionalId, cursoId)
            .Where(a => a.Intervalo.Fim > de)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Realizados = g.Count(a => a.Estado == EstadoAtendimento.Realizado && a.Intervalo.Inicio >= de),
                // Segundos ÷ 60 (DATEDIFF(minute) contaria viradas de minuto); IntervaloHorario.Criar já grava minutos inteiros.
                Minutos = g.Sum(a => a.Estado == EstadoAtendimento.Realizado
                    ? EF.Functions.DateDiffSecond(a.Intervalo.Inicio < de ? de : a.Intervalo.Inicio, a.Intervalo.Fim > ate ? ate : a.Intervalo.Fim) / 60
                    : 0),
                Individuais = g.Count(a => a.Estado == EstadoAtendimento.Realizado && a.Tipo != TipoAtendimento.Reuniao && a.Intervalo.Inicio >= de),
                Faltas = g.Count(a => a.Estado == EstadoAtendimento.Ausente && a.Tipo != TipoAtendimento.Reuniao && a.Intervalo.Inicio >= de),
            })
            .SingleOrDefaultAsync(ct);

        var semDesfecho = await SemDesfecho(db, relogio, profissionalId, cursoId)
            .CountAsync(c => c.Intervalo.Inicio >= de && c.Intervalo.Inicio < ate, ct);
        return new SemanaBrutaDto(bruto?.Realizados ?? 0, bruto?.Minutos ?? 0, bruto?.Individuais ?? 0, bruto?.Faltas ?? 0, semDesfecho);
    }

    public Task<IReadOnlyList<CompromissoDto>> ObterAgendaDoDiaAsync(DateOnly dia, Guid? profissionalId, CancellationToken ct) =>
        AgendaQueries.Listar(db, relogio.Fuso.InicioDoDia(dia), relogio.Fuso.InicioDoDia(dia.AddDays(1)), profissionalId, ct);

    public async Task<IReadOnlyList<SerieMensalDto>> ObterSerieMensalAsync(DateOnly mesInicial, DateOnly mesFinal, Guid? profissionalId,
        Guid? cursoId, CancellationToken ct)
    {
        var fuso = relogio.Fuso;
        var de = fuso.InicioDoDia(new DateOnly(mesInicial.Year, mesInicial.Month, 1));
        var ate = fuso.InicioDoDia(new DateOnly(mesFinal.Year, mesFinal.Month, 1).AddMonths(1));
        // Mesma regra da produção: conta no mês do início; o trecho que vira o mês soma minutos no seguinte.
        // Repartido em memória: só início e fim dos realizados de seis meses.
        var intervalos = await Atendimentos(db, relogio, de.AddHours(-DuracaoMaximaHoras), ate, profissionalId, cursoId)
            .Where(a => a.Estado == EstadoAtendimento.Realizado && a.Intervalo.Fim > de)
            .Select(a => new { a.Intervalo.Inicio, a.Intervalo.Fim })
            .ToListAsync(ct);
        var meses = new SortedDictionary<(int Ano, int Mes), (int Total, int Minutos)>();
        foreach (var a in intervalos)
        {
            var inicio = a.Inicio < de ? de : a.Inicio;
            var fim = a.Fim > ate ? ate : a.Fim;
            var conta = a.Inicio >= de ? 1 : 0;
            while (inicio < fim)
            {
                var data = fuso.DataLocal(inicio);
                var virada = fuso.InicioDoDia(new DateOnly(data.Year, data.Month, 1).AddMonths(1));
                var corte = fim < virada ? fim : virada;
                var (total, minutos) = meses.GetValueOrDefault((data.Year, data.Month));
                meses[(data.Year, data.Month)] = (total + conta, minutos + (int)(corte - inicio).TotalMinutes);
                conta = 0;
                inicio = corte;
            }
        }
        return meses.Select(m => new SerieMensalDto(m.Key.Ano, m.Key.Mes, m.Value.Total, m.Value.Minutos, false)).ToList();
    }

    public async Task<IReadOnlyList<ContagemDto>> ContarAlunosPorCursoAsync(Guid? cursoId, CancellationToken ct) =>
        (await db.Cursos.AsNoTracking()
            .Where(c => cursoId == null || c.Id == cursoId)
            .Select(c => new { c.Id, c.Nome, Total = db.Alunos.Count(a => a.CursoAtualId == c.Id) })
            .Where(c => c.Total > 0)
            .OrderBy(c => c.Nome).ThenBy(c => c.Id)
            .ToListAsync(ct))
        .Select(c => new ContagemDto(c.Id.ToString(), c.Nome, c.Total))
        .ToList();

    public async Task<IReadOnlyList<ContagemDto>> ContarAlunosPorSituacaoAsync(Guid? cursoId, CancellationToken ct) =>
        (await db.Alunos.AsNoTracking()
            .Where(a => cursoId == null || a.CursoAtualId == cursoId)
            .GroupBy(a => a.Situacao)
            .Select(g => new { Situacao = g.Key, Total = g.Count() })
            .ToListAsync(ct))
        .OrderBy(s => s.Situacao)
        .Select(s => new ContagemDto(s.Situacao.ToString(), Rotulos.De(s.Situacao), s.Total))
        .ToList();

    /// <summary>
    /// <c>faltas_recorrentes</c>: as últimas N avaliações individuais (Realizado/Ausente, por início decrescente) todas
    /// ausentes, ou ao menos M faltas nos últimos D dias (opções). Com profissional, só os atendimentos dele contam.
    /// <c>sem_desfecho</c>: compromissos passados ainda Agendados (atendimentos, atividades, reuniões e tarefas), sem registro ou realização.
    /// </summary>
    public async Task<IReadOnlyList<AlertaDto>> ObterAlertasAsync(DateOnly hoje, Guid? profissionalId, Guid? cursoId, ConfiguracaoAlertasDto opcoes,
        CancellationToken ct)
    {
        var inicioJanela = relogio.Fuso.InicioDoDia(hoje.AddDays(-opcoes.JanelaDias + 1));
        var fimJanela = relogio.Fuso.InicioDoDia(hoje.AddDays(1));
        // Só até o fim de "hoje": registros posteriores não entram nas últimas N nem na janela.
        var individuais = db.Atendimentos.AsNoTracking()
            .Where(a => a.Tipo != TipoAtendimento.Reuniao && a.Intervalo.Inicio < fimJanela
                && (a.Estado == EstadoAtendimento.Realizado || a.Estado == EstadoAtendimento.Ausente)
                && (profissionalId == null || a.ProfissionalId == profissionalId));
        // Alta ou inativo não volta a ser atendido: as faltas antigas nunca seriam substituídas e o alerta ficaria para sempre.
        var alunos = db.Alunos.AsNoTracking().Where(a => (cursoId == null || a.CursoAtualId == cursoId)
            && (a.Situacao == SituacaoAluno.Ativo || a.Situacao == SituacaoAluno.EmAcompanhamento));

        // Candidato: a avaliação mais recente é falta (condição das consecutivas) ou há falta na janela. Sem esse corte,
        // qualquer aluno com uma falta antiga entraria e pagaria as duas subconsultas abaixo.
        var candidatos = await alunos
            .Where(al => individuais.Where(a => a.AlunoId == al.Id)
                    .OrderByDescending(a => a.Intervalo.Inicio).ThenByDescending(a => a.Id)
                    .Select(a => (EstadoAtendimento?)a.Estado).FirstOrDefault() == EstadoAtendimento.Ausente
                || individuais.Any(a => a.AlunoId == al.Id && a.Estado == EstadoAtendimento.Ausente
                    && a.Intervalo.Inicio >= inicioJanela && a.Intervalo.Inicio < fimJanela))
            .OrderBy(al => al.NomeCompleto).ThenBy(al => al.Id)
            .Select(al => new
            {
                al.Id, al.NomeCompleto,
                Ultimos = individuais.Where(a => a.AlunoId == al.Id)
                    .OrderByDescending(a => a.Intervalo.Inicio).ThenByDescending(a => a.Id)
                    .Take(opcoes.FaltasConsecutivas).Select(a => a.Estado).ToList(),
                NaJanela = individuais.Count(a => a.AlunoId == al.Id && a.Estado == EstadoAtendimento.Ausente
                    && a.Intervalo.Inicio >= inicioJanela && a.Intervalo.Inicio < fimJanela),
            })
            .ToListAsync(ct);

        var faltas = new List<AlertaDto>();
        foreach (var c in candidatos)
        {
            if (c.Ultimos.Count == opcoes.FaltasConsecutivas && c.Ultimos.All(e => e == EstadoAtendimento.Ausente))
                faltas.Add(new AlertaDto("faltas_recorrentes", "Faltas recorrentes",
                    $"{c.NomeCompleto}: {opcoes.FaltasConsecutivas} faltas consecutivas", c.Id, opcoes.FaltasConsecutivas));
            else if (c.NaJanela >= opcoes.FaltasJanela)
                faltas.Add(new AlertaDto("faltas_recorrentes", "Faltas recorrentes",
                    $"{c.NomeCompleto}: {c.NaJanela} faltas nos últimos {opcoes.JanelaDias} dias", c.Id, c.NaJanela));
        }
        var alertas = Limitados(faltas.Take(LimitePorTipo), faltas.Count, "faltas_recorrentes", "Faltas recorrentes");

        var semDesfecho = await SemDesfecho(db, relogio, profissionalId, cursoId).CountAsync(ct);
        if (semDesfecho > 0)
            alertas.Add(new AlertaDto("sem_desfecho", "Compromissos sem desfecho",
                semDesfecho == 1 ? "1 compromisso agendado já passou sem registro" : $"{semDesfecho} compromissos agendados já passaram sem registro", null, semDesfecho));
        return alertas;
    }

    public async Task<EncaminhamentosPainelDto> ContarEncaminhamentosAsync(EscopoEncaminhamentos e, Guid? solicitanteId, DateOnly hoje,
        ConfiguracaoAlertasDto opcoes, CancellationToken ct)
    {
        var limiteAcolhimento = hoje.AddDays(-opcoes.DiasSemAcolhimento);
        // AtualizadoEm acompanha a criação e cada evolução: equivale a max(CriadoEm, última evolução).
        var limiteAtualizacao = relogio.Fuso.InicioDoDia(hoje.AddDays(-opcoes.DiasSemAtualizacao));
        var c = await Abertos(e, solicitanteId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Abertos = g.Count(),
                Aguardando = g.Count(x => x.Estado == EstadoEncaminhamento.Encaminhado),
                EmAtendimento = g.Count(x => x.Estado == EstadoEncaminhamento.EmAtendimento),
                SemAtualizacao = g.Count(x => x.AtualizadoEm < limiteAtualizacao),
                SemAcolhimento = g.Count(x => x.Estado == EstadoEncaminhamento.Encaminhado && x.DataEncaminhamento < limiteAcolhimento),
            })
            .SingleOrDefaultAsync(ct);
        return c is null
            ? new(0, 0, 0, 0, 0, opcoes.DiasSemAtualizacao)
            : new(c.Abertos, c.Aguardando, c.EmAtendimento, c.SemAtualizacao, c.SemAcolhimento, opcoes.DiasSemAtualizacao);
    }

    public async Task<IReadOnlyList<AlertaDto>> ObterAlertasEncaminhamentoAsync(EscopoEncaminhamentos e, Guid? solicitanteId,
        bool incluirSemDestinatario, DateOnly hoje, ConfiguracaoAlertasDto opcoes, CancellationToken ct)
    {
        var limite = hoje.AddDays(-opcoes.DiasSemAcolhimento);
        var qSemAcolhimento = Abertos(e, solicitanteId)
            .Where(x => x.Estado == EstadoEncaminhamento.Encaminhado && x.DataEncaminhamento < limite);
        var totalSemAcolhimento = await qSemAcolhimento.CountAsync(ct);
        var semAcolhimento = await qSemAcolhimento
            .OrderBy(x => x.DataEncaminhamento).ThenBy(x => x.Id).Take(LimitePorTipo)
            .Select(x => new
            {
                x.DataEncaminhamento, x.AlunoId,
                Aluno = db.Alunos.Where(a => a.Id == x.AlunoId).Select(a => a.NomeCompleto).FirstOrDefault() ?? "",
            })
            .ToListAsync(ct);
        var alertas = Limitados(semAcolhimento
            .Select(x =>
            {
                var dias = hoje.DayNumber - x.DataEncaminhamento.DayNumber;
                return new AlertaDto("encaminhamento-sem-acolhimento", "Encaminhamento sem acolhimento",
                    $"{x.Aluno}: encaminhado há {dias} dias sem acolhimento", x.AlunoId, dias);
            }), totalSemAcolhimento, "encaminhamento-sem-acolhimento", "Encaminhamento sem acolhimento");
        if (!incluirSemDestinatario) return alertas;

        var qSemDestinatario = Abertos(e, solicitanteId)
            .Where(x => ((x.Destino == DestinoEncaminhamento.Sip || x.Destino == DestinoEncaminhamento.ClinicaEscola)
                    && !db.Users.Any(u => u.Ativo && u.Perfil == Perfis.Sip))
                || (x.Destino == DestinoEncaminhamento.CoordenacaoCurso
                    && !db.Users.Any(u => u.Ativo && u.Perfil == Perfis.CoordenadorCurso && u.CursoId == x.CursoId)));
        var totalSemDestinatario = await qSemDestinatario.CountAsync(ct);
        var semDestinatario = await qSemDestinatario
            .OrderBy(x => x.DataEncaminhamento).ThenBy(x => x.Id).Take(LimitePorTipo)
            .Select(x => new
            {
                x.DataEncaminhamento, x.Destino, x.AlunoId,
                Aluno = db.Alunos.Where(a => a.Id == x.AlunoId).Select(a => a.NomeCompleto).FirstOrDefault() ?? "",
            })
            .ToListAsync(ct);
        alertas.AddRange(Limitados(semDestinatario.Select(x => new AlertaDto("encaminhamento-sem-destinatario",
            "Encaminhamento sem destinatário",
            $"{x.Aluno}: nenhum usuário ativo para receber o encaminhamento ({Rotulos.De(x.Destino)})", x.AlunoId,
            hoje.DayNumber - x.DataEncaminhamento.DayNumber)), totalSemDestinatario, "encaminhamento-sem-destinatario",
            "Encaminhamento sem destinatário"));
        return alertas;
    }

    public async Task<PeisPainelDto> ContarPeisAsync(EscopoPeis e, Guid? responsavelId, Guid? cursoId, CancellationToken ct)
    {
        var c = await PeiQueries.Visiveis(db, VersoesDosPeis(responsavelId, cursoId), e)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                EmElaboracao = g.Count(v => v.Estado == EstadoVersaoPei.Rascunho || v.Estado == EstadoVersaoPei.EmRevisao),
                Aguardando = g.Count(v => v.Estado == EstadoVersaoPei.AguardandoAssinaturas),
                // Ciência só vem de coordenador do curso da publicação: "sem ciência" independe de quem consulta.
                SemCiencia = g.Count(v => v.Estado == EstadoVersaoPei.Publicada && !v.Ciencias.Any()),
            })
            .SingleOrDefaultAsync(ct);
        return c is null ? new(0, 0, 0) : new(c.EmElaboracao, c.Aguardando, c.SemCiencia);
    }

    /// <summary>
    /// <c>pei-pendente</c>: versão em revisão ou aguardando assinaturas há mais de N dias desde o envio para revisão.
    /// <c>pei-sem-coordenador</c>: versão publicada sem coordenador de curso ativo no curso da publicação.
    /// Nunca seleciona a demanda interna.
    /// </summary>
    public async Task<IReadOnlyList<AlertaDto>> ObterAlertasPeiAsync(Guid? responsavelId, Guid? cursoId, bool incluirSemCoordenador, DateOnly hoje,
        ConfiguracaoAlertasDto opcoes, CancellationToken ct)
    {
        var limite = relogio.Fuso.InicioDoDia(hoje.AddDays(-opcoes.DiasPeiPendente));
        var qPendentes = VersoesDosPeis(responsavelId, cursoId)
            .Where(v => (v.Estado == EstadoVersaoPei.EmRevisao || v.Estado == EstadoVersaoPei.AguardandoAssinaturas)
                && v.EnviadaRevisaoEm < limite);
        var totalPendentes = await qPendentes.CountAsync(ct);
        var pendentes = await qPendentes
            .OrderBy(v => v.EnviadaRevisaoEm).ThenBy(v => v.Id).Take(LimitePorTipo)
            .Select(v => new
            {
                v.Numero, v.Estado, v.EnviadaRevisaoEm,
                Pei = db.Peis.Where(p => p.Id == v.PeiId).Select(p => new
                {
                    p.AlunoId,
                    Aluno = db.Alunos.Where(a => a.Id == p.AlunoId).Select(a => a.NomeCompleto).FirstOrDefault() ?? "",
                }).First(),
            })
            .ToListAsync(ct);
        var alertas = Limitados(pendentes
            .Select(x =>
            {
                var dias = hoje.DayNumber - relogio.Fuso.DataLocal(x.EnviadaRevisaoEm!.Value).DayNumber;
                var situacao = x.Estado == EstadoVersaoPei.EmRevisao ? "em revisão" : "aguardando assinaturas";
                return new AlertaDto("pei-pendente", "PEI pendente", $"{x.Pei.Aluno}: PEI versão {x.Numero} {situacao} há {dias} dias",
                    x.Pei.AlunoId, dias);
            }), totalPendentes, "pei-pendente", "PEI pendente");
        if (!incluirSemCoordenador) return alertas;

        var qSemCoordenador = VersoesDosPeis(responsavelId, cursoId)
            .Where(v => v.Estado == EstadoVersaoPei.Publicada
                && !db.Users.Any(u => u.Ativo && u.Perfil == Perfis.CoordenadorCurso && u.CursoId == v.CursoPublicacaoId));
        var totalSemCoordenador = await qSemCoordenador.CountAsync(ct);
        var semCoordenador = await qSemCoordenador
            .OrderBy(v => v.PublicadaEm).ThenBy(v => v.Id).Take(LimitePorTipo)
            .Select(v => new
            {
                v.Numero, v.PublicadaEm,
                Pei = db.Peis.Where(p => p.Id == v.PeiId).Select(p => new
                {
                    p.AlunoId,
                    Aluno = db.Alunos.Where(a => a.Id == p.AlunoId).Select(a => a.NomeCompleto).FirstOrDefault() ?? "",
                }).First(),
            })
            .ToListAsync(ct);
        alertas.AddRange(Limitados(semCoordenador.Select(x => new AlertaDto("pei-sem-coordenador", "PEI sem coordenador",
            $"{x.Pei.Aluno}: nenhum coordenador ativo no curso para receber o PEI publicado (versão {x.Numero})", x.Pei.AlunoId,
            hoje.DayNumber - relogio.Fuso.DataLocal(x.PublicadaEm!.Value).DayNumber)), totalSemCoordenador, "pei-sem-coordenador",
            "PEI sem coordenador"));
        return alertas;
    }

    // Versão publicada (ou arquivada) pertence ao curso gravado na publicação; a ainda aberta, ao curso atual do aluno,
    // que é para onde ela será publicada.
    private IQueryable<VersaoPei> VersoesDosPeis(Guid? responsavelId, Guid? cursoId) =>
        db.Peis.AsNoTracking().Where(p => responsavelId == null || p.ProfissionalResponsavelId == responsavelId)
            .SelectMany(p => p.Versoes.Where(v => cursoId == null || v.CursoPublicacaoId == cursoId
                || (v.CursoPublicacaoId == null && db.Alunos.Any(al => al.Id == p.AlunoId && al.CursoAtualId == cursoId))));

    public async Task<AtividadesPainelDto> ObterAtividadesAsync(DateOnly hoje, Guid? profissionalId, Guid? cursoId, CancellationToken ct)
    {
        var mes = new DateOnly(hoje.Year, hoje.Month, 1);
        var de = relogio.Fuso.InicioDoDia(mes);
        var ate = relogio.Fuso.InicioDoDia(mes.AddMonths(1));
        var q = db.AtividadesCurso.AsNoTracking().Where(a => a.Intervalo.Inicio >= de && a.Intervalo.Inicio < ate);
        if (profissionalId is { } prof) q = q.Where(a => a.ProfissionalId == prof);
        if (cursoId is { } curso) q = q.Where(a => a.CursoId == curso);

        var totais = await q.GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Minutos = g.Sum(a => EF.Functions.DateDiffSecond(a.Intervalo.Inicio, a.Intervalo.Fim) / 60) })
            .SingleOrDefaultAsync(ct);
        var linhas = await q.OrderByDescending(a => a.Intervalo.Inicio).ThenByDescending(a => a.Id).Take(5)
            .Select(a => new
            {
                a.Id, a.Intervalo.Inicio,
                Minutos = EF.Functions.DateDiffSecond(a.Intervalo.Inicio, a.Intervalo.Fim) / 60,
                Curso = db.Cursos.Where(c => c.Id == a.CursoId).Select(c => c.Nome).FirstOrDefault(),
                Profissional = db.Users.Where(u => u.Id == a.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                a.Tipo, a.Descricao,
            })
            .ToListAsync(ct);
        var recentes = linhas.Select(l => new AtividadeResumoDto(l.Id, l.Inicio, AtividadeCurso.TurnoDe(relogio.Fuso.HoraLocal(l.Inicio)),
            l.Minutos, l.Curso, l.Profissional, l.Tipo, l.Descricao)).ToList();
        return new AtividadesPainelDto(totais?.Total ?? 0, totais?.Minutos ?? 0, recentes);
    }

    // Mesma janela e mesmo recorte de minutos de ObterSemanaAsync: a soma por profissional fecha com o total da semana.
    public async Task<IReadOnlyList<ProducaoProfissionalDto>> ObterPorProfissionalAsync(DateOnly inicio, DateOnly fim, Guid? cursoId,
        CancellationToken ct)
    {
        var de = relogio.Fuso.InicioDoDia(inicio);
        var ate = relogio.Fuso.InicioDoDia(fim.AddDays(1));
        return await Atendimentos(db, relogio, de.AddHours(-DuracaoMaximaHoras), ate, null, cursoId)
            .Where(a => a.Intervalo.Fim > de)
            .GroupBy(a => a.ProfissionalId)
            .Select(g => new
            {
                Id = g.Key,
                Nome = db.Users.Where(u => u.Id == g.Key).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                Realizados = g.Count(a => a.Estado == EstadoAtendimento.Realizado && a.Intervalo.Inicio >= de),
                Faltas = g.Count(a => a.Estado == EstadoAtendimento.Ausente && a.Tipo != TipoAtendimento.Reuniao && a.Intervalo.Inicio >= de),
                Minutos = g.Sum(a => a.Estado == EstadoAtendimento.Realizado
                    ? EF.Functions.DateDiffSecond(a.Intervalo.Inicio < de ? de : a.Intervalo.Inicio, a.Intervalo.Fim > ate ? ate : a.Intervalo.Fim) / 60
                    : 0),
            })
            .OrderBy(p => p.Nome).ThenBy(p => p.Id)
            .Select(p => new ProducaoProfissionalDto(p.Id, p.Nome, p.Realizados, p.Faltas, p.Minutos))
            .ToListAsync(ct);
    }

    private IQueryable<Encaminhamento> Abertos(EscopoEncaminhamentos e, Guid? solicitanteId) =>
        EncaminhamentoQueries.NoEscopo(db, e)
            .Where(x => x.Estado != EstadoEncaminhamento.Alta && x.Estado != EstadoEncaminhamento.Desistencia
                && x.Estado != EstadoEncaminhamento.Cancelado && (solicitanteId == null || x.SolicitanteId == solicitanteId));

    /// <summary>
    /// Filtro de curso pelo vínculo vigente na data civil local do início ([Desde, Ate): o dia da transferência já é do curso
    /// novo), não pelo curso atual. Data anterior ao início do acompanhamento (registro retroativo) conta para o primeiro
    /// vínculo; vínculo de duração zero não vale. Compartilhada com a produção.
    /// </summary>
    internal static IQueryable<Atendimento> Atendimentos(NapDbContext db, IRelogio relogio, DateTimeOffset de, DateTimeOffset ate,
        Guid? profissionalId, Guid? cursoId)
    {
        var q = db.Atendimentos.AsNoTracking()
            .Where(a => a.Intervalo.Inicio >= de && a.Intervalo.Inicio < ate
                && (profissionalId == null || a.ProfissionalId == profissionalId));
        return cursoId is { } curso
            ? q.Where(NoCursoNaData<Atendimento>(db, a => a.AlunoId, a => a.Intervalo.Inicio, curso, FusoSql(relogio.Fuso)))
            : q;
    }

    /// <summary>Ainda sem desfecho, terminado ou não: base de <see cref="SemDesfecho"/> e da trava do fechamento semanal.</summary>
    internal static IQueryable<Compromisso> AguardandoDesfecho(NapDbContext db, Guid? profissionalId) =>
        db.Compromissos.AsNoTracking()
            // Todo tipo tem desfecho, menos bloqueio e lembrete: um tipo novo entra sem precisar lembrar desta lista.
            .Where(c => c.Tipo != TipoCompromisso.Bloqueio && c.Tipo != TipoCompromisso.Lembrete
                && c.Estado == EstadoCompromisso.Agendado && c.RegistroVinculadoId == null
                && (profissionalId == null || c.ProfissionalId == profissionalId));

    /// <summary>Pendente de classificação; compartilhada com o fechamento semanal. Curso pelo vínculo vigente, como em <see cref="Atendimentos"/>.</summary>
    internal static IQueryable<Compromisso> SemDesfecho(NapDbContext db, IRelogio relogio, Guid? profissionalId, Guid? cursoId)
    {
        var agora = relogio.Agora;
        var q = AguardandoDesfecho(db, profissionalId).Where(c => c.Intervalo.Fim < agora);
        return cursoId is { } curso
            ? q.Where(NoCursoNaData<Compromisso>(db, c => c.AlunoId, c => c.Intervalo.Inicio, curso, FusoSql(relogio.Fuso)))
            : q;
    }

    /// <summary>
    /// Regra única do vínculo vigente (ver <see cref="Atendimentos"/>) sobre qualquer registro com aluno e instante: a mesma
    /// árvore vale para atendimentos e compromissos, sem duas cópias que precisem mudar juntas.
    /// </summary>
    private static Expression<Func<T, bool>> NoCursoNaData<T>(NapDbContext db, Expression<Func<T, Guid?>> aluno,
        Expression<Func<T, DateTimeOffset>> instante, Guid cursoId, string fuso)
    {
        Expression<Func<Guid?, DateTimeOffset, bool>> regra = (alunoId, inicio) => db.Alunos.Any(al => al.Id == alunoId
            && al.HistoricoCursos.Any(v => v.CursoId == cursoId
                && (v.Desde <= DateOnly.FromDateTime(EF.Functions.AtTimeZone(inicio, fuso).Date)
                    || (v.Desde == al.InicioAcompanhamento && v.Ate != v.Desde))
                && (v.Ate == null || DateOnly.FromDateTime(EF.Functions.AtTimeZone(inicio, fuso).Date) < v.Ate)));
        var registro = aluno.Parameters[0];
        var corpo = ReplacingExpressionVisitor.Replace(regra.Parameters[0], aluno.Body, regra.Body);
        corpo = ReplacingExpressionVisitor.Replace(regra.Parameters[1],
            ReplacingExpressionVisitor.Replace(instante.Parameters[0], registro, instante.Body), corpo);
        return Expression.Lambda<Func<T, bool>>(corpo, registro);
    }

    /// <summary>Nome do fuso para o AT TIME ZONE do SQL Server, que só conhece ids do Windows.</summary>
    private static string FusoSql(TimeZoneInfo fuso) =>
        TimeZoneInfo.TryConvertIanaIdToWindowsId(fuso.Id, out var windows) ? windows : fuso.Id;
}
