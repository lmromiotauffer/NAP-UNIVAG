using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Configuracoes;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Painel;
using Nap.Application.Features.Peis;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Atividades;
using Nap.Domain.Comum;
using Nap.Domain.Encaminhamentos;
using Nap.Domain.Peis;

namespace Nap.Infrastructure.Persistencia.Consultas;

/// <summary>Números brutos do painel. Faltas contam só atendimentos individuais (reuniões ficam fora da base da taxa).</summary>
public sealed class PainelQueries(NapDbContext db, IRelogio relogio) : IPainelQueries
{
    public async Task<SemanaBrutaDto> ObterSemanaAsync(DateOnly inicio, DateOnly fim, Guid? profissionalId, Guid? cursoId, CancellationToken ct)
    {
        var de = relogio.Fuso.InicioDoDia(inicio);
        var ate = relogio.Fuso.InicioDoDia(fim.AddDays(1));
        var bruto = await Atendimentos(db, relogio, de, ate, profissionalId, cursoId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Realizados = g.Count(a => a.Estado == EstadoAtendimento.Realizado),
                // Segundos ÷ 60, como a CalculadoraProducao (DATEDIFF(minute) contaria viradas de minuto).
                Minutos = g.Sum(a => a.Estado == EstadoAtendimento.Realizado ? EF.Functions.DateDiffSecond(a.Intervalo.Inicio, a.Intervalo.Fim) / 60 : 0),
                Individuais = g.Count(a => a.Estado == EstadoAtendimento.Realizado && a.Tipo != TipoAtendimento.Reuniao),
                Faltas = g.Count(a => a.Estado == EstadoAtendimento.Ausente && a.Tipo != TipoAtendimento.Reuniao),
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
        var de = relogio.Fuso.InicioDoDia(new DateOnly(mesInicial.Year, mesInicial.Month, 1));
        var ate = relogio.Fuso.InicioDoDia(new DateOnly(mesFinal.Year, mesFinal.Month, 1).AddMonths(1));
        var fuso = FusoSql(relogio.Fuso);
        // Mês civil no fuso institucional, agrupado no banco (AT TIME ZONE, como em Atendimentos).
        return await Atendimentos(db, relogio, de, ate, profissionalId, cursoId)
            .Where(a => a.Estado == EstadoAtendimento.Realizado)
            .GroupBy(a => new
            {
                EF.Functions.AtTimeZone(a.Intervalo.Inicio, fuso).Year,
                EF.Functions.AtTimeZone(a.Intervalo.Inicio, fuso).Month,
            })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new SerieMensalDto(g.Key.Year, g.Key.Month, g.Count(),
                g.Sum(a => EF.Functions.DateDiffSecond(a.Intervalo.Inicio, a.Intervalo.Fim) / 60), false))
            .ToListAsync(ct);
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
        var alunos = db.Alunos.AsNoTracking().Where(a => cursoId == null || a.CursoAtualId == cursoId);

        var candidatos = await alunos
            .Where(al => individuais.Any(a => a.AlunoId == al.Id && a.Estado == EstadoAtendimento.Ausente))
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

        var alertas = new List<AlertaDto>();
        foreach (var c in candidatos)
        {
            if (c.Ultimos.Count == opcoes.FaltasConsecutivas && c.Ultimos.All(e => e == EstadoAtendimento.Ausente))
                alertas.Add(new AlertaDto("faltas_recorrentes", "Faltas recorrentes",
                    $"{c.NomeCompleto}: {opcoes.FaltasConsecutivas} faltas consecutivas", c.Id, opcoes.FaltasConsecutivas));
            else if (c.NaJanela >= opcoes.FaltasJanela)
                alertas.Add(new AlertaDto("faltas_recorrentes", "Faltas recorrentes",
                    $"{c.NomeCompleto}: {c.NaJanela} faltas nos últimos {opcoes.JanelaDias} dias", c.Id, c.NaJanela));
        }

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
        var semAcolhimento = await Abertos(e, solicitanteId)
            .Where(x => x.Estado == EstadoEncaminhamento.Encaminhado && x.DataEncaminhamento < limite)
            .OrderBy(x => x.DataEncaminhamento).ThenBy(x => x.Id)
            .Select(x => new
            {
                x.DataEncaminhamento, x.AlunoId,
                Aluno = db.Alunos.Where(a => a.Id == x.AlunoId).Select(a => a.NomeCompleto).FirstOrDefault() ?? "",
            })
            .ToListAsync(ct);
        var alertas = semAcolhimento
            .Select(x =>
            {
                var dias = hoje.DayNumber - x.DataEncaminhamento.DayNumber;
                return new AlertaDto("encaminhamento-sem-acolhimento", "Encaminhamento sem acolhimento",
                    $"{x.Aluno}: encaminhado há {dias} dias sem acolhimento", x.AlunoId, dias);
            })
            .ToList();
        if (!incluirSemDestinatario) return alertas;

        var semDestinatario = await Abertos(e, solicitanteId)
            .Where(x => ((x.Destino == DestinoEncaminhamento.Sip || x.Destino == DestinoEncaminhamento.ClinicaEscola)
                    && !db.Users.Any(u => u.Ativo && u.Perfil == Perfis.Sip))
                || (x.Destino == DestinoEncaminhamento.CoordenacaoCurso
                    && !db.Users.Any(u => u.Ativo && u.Perfil == Perfis.CoordenadorCurso && u.CursoId == x.CursoId)))
            .OrderBy(x => x.DataEncaminhamento).ThenBy(x => x.Id)
            .Select(x => new
            {
                x.DataEncaminhamento, x.Destino, x.AlunoId,
                Aluno = db.Alunos.Where(a => a.Id == x.AlunoId).Select(a => a.NomeCompleto).FirstOrDefault() ?? "",
            })
            .ToListAsync(ct);
        alertas.AddRange(semDestinatario.Select(x => new AlertaDto("encaminhamento-sem-destinatario", "Encaminhamento sem destinatário",
            $"{x.Aluno}: nenhum usuário ativo para receber o encaminhamento ({Rotulos.De(x.Destino)})", x.AlunoId,
            hoje.DayNumber - x.DataEncaminhamento.DayNumber)));
        return alertas;
    }

    public async Task<PeisPainelDto> ContarPeisAsync(EscopoPeis e, Guid? responsavelId, Guid? cursoId, CancellationToken ct)
    {
        var c = await PeiQueries.Visiveis(VersoesDosPeis(responsavelId, cursoId), e)
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
        var pendentes = await VersoesDosPeis(responsavelId, cursoId)
            .Where(v => (v.Estado == EstadoVersaoPei.EmRevisao || v.Estado == EstadoVersaoPei.AguardandoAssinaturas)
                && v.EnviadaRevisaoEm < limite)
            .OrderBy(v => v.EnviadaRevisaoEm).ThenBy(v => v.Id)
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
        var alertas = pendentes
            .Select(x =>
            {
                var dias = hoje.DayNumber - relogio.Fuso.DataLocal(x.EnviadaRevisaoEm!.Value).DayNumber;
                var situacao = x.Estado == EstadoVersaoPei.EmRevisao ? "em revisão" : "aguardando assinaturas";
                return new AlertaDto("pei-pendente", "PEI pendente", $"{x.Pei.Aluno}: PEI versão {x.Numero} {situacao} há {dias} dias",
                    x.Pei.AlunoId, dias);
            })
            .ToList();
        if (!incluirSemCoordenador) return alertas;

        var semCoordenador = await VersoesDosPeis(responsavelId, cursoId)
            .Where(v => v.Estado == EstadoVersaoPei.Publicada
                && !db.Users.Any(u => u.Ativo && u.Perfil == Perfis.CoordenadorCurso && u.CursoId == v.CursoPublicacaoId))
            .OrderBy(v => v.PublicadaEm).ThenBy(v => v.Id)
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
        alertas.AddRange(semCoordenador.Select(x => new AlertaDto("pei-sem-coordenador", "PEI sem coordenador",
            $"{x.Pei.Aluno}: nenhum coordenador ativo no curso para receber o PEI publicado (versão {x.Numero})", x.Pei.AlunoId,
            hoje.DayNumber - relogio.Fuso.DataLocal(x.PublicadaEm!.Value).DayNumber)));
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
        var recentes = linhas.Select(l => new AtividadeResumoDto(l.Id, l.Inicio,
            AtividadeCurso.TurnoDe(TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(l.Inicio, relogio.Fuso).DateTime)),
            l.Minutos, l.Curso, l.Profissional, l.Tipo, l.Descricao)).ToList();
        return new AtividadesPainelDto(totais?.Total ?? 0, totais?.Minutos ?? 0, recentes);
    }

    public async Task<IReadOnlyList<ProducaoProfissionalDto>> ObterPorProfissionalAsync(DateOnly inicio, DateOnly fim, Guid? cursoId,
        CancellationToken ct) =>
        await Atendimentos(db, relogio, relogio.Fuso.InicioDoDia(inicio), relogio.Fuso.InicioDoDia(fim.AddDays(1)), null, cursoId)
            .GroupBy(a => a.ProfissionalId)
            .Select(g => new
            {
                Id = g.Key,
                Nome = db.Users.Where(u => u.Id == g.Key).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                Realizados = g.Count(a => a.Estado == EstadoAtendimento.Realizado),
                Faltas = g.Count(a => a.Estado == EstadoAtendimento.Ausente && a.Tipo != TipoAtendimento.Reuniao),
                Minutos = g.Sum(a => a.Estado == EstadoAtendimento.Realizado ? EF.Functions.DateDiffSecond(a.Intervalo.Inicio, a.Intervalo.Fim) / 60 : 0),
            })
            .OrderBy(p => p.Nome).ThenBy(p => p.Id)
            .Select(p => new ProducaoProfissionalDto(p.Id, p.Nome, p.Realizados, p.Faltas, p.Minutos))
            .ToListAsync(ct);

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
        var fuso = FusoSql(relogio.Fuso);
        return db.Atendimentos.AsNoTracking()
            .Where(a => a.Intervalo.Inicio >= de && a.Intervalo.Inicio < ate
                && (profissionalId == null || a.ProfissionalId == profissionalId)
                && (cursoId == null || db.Alunos.Any(al => al.Id == a.AlunoId && al.HistoricoCursos.Any(v => v.CursoId == cursoId
                    && (v.Desde <= DateOnly.FromDateTime(EF.Functions.AtTimeZone(a.Intervalo.Inicio, fuso).Date)
                        || (v.Desde == al.InicioAcompanhamento && v.Ate != v.Desde))
                    && (v.Ate == null || DateOnly.FromDateTime(EF.Functions.AtTimeZone(a.Intervalo.Inicio, fuso).Date) < v.Ate)))));
    }

    /// <summary>Pendente de classificação; compartilhada com o fechamento semanal. Curso pelo vínculo vigente, como em <see cref="Atendimentos"/>.</summary>
    internal static IQueryable<Compromisso> SemDesfecho(NapDbContext db, IRelogio relogio, Guid? profissionalId, Guid? cursoId)
    {
        var agora = relogio.Agora;
        var fuso = FusoSql(relogio.Fuso);
        return db.Compromissos.AsNoTracking()
            .Where(c => (c.Tipo == TipoCompromisso.Atendimento || c.Tipo == TipoCompromisso.AtividadeCurso
                    || c.Tipo == TipoCompromisso.ReuniaoEquipe || c.Tipo == TipoCompromisso.ReuniaoCoordenacaoGestao
                    || c.Tipo == TipoCompromisso.ElaboracaoPei || c.Tipo == TipoCompromisso.TarefaAdministrativa
                    || c.Tipo == TipoCompromisso.EncaminhamentoDevolutiva)
                && c.Estado == EstadoCompromisso.Agendado
                && c.RegistroVinculadoId == null && c.Intervalo.Fim < agora
                && (profissionalId == null || c.ProfissionalId == profissionalId)
                && (cursoId == null || db.Alunos.Any(al => al.Id == c.AlunoId && al.HistoricoCursos.Any(v => v.CursoId == cursoId
                    && (v.Desde <= DateOnly.FromDateTime(EF.Functions.AtTimeZone(c.Intervalo.Inicio, fuso).Date)
                        || (v.Desde == al.InicioAcompanhamento && v.Ate != v.Desde))
                    && (v.Ate == null || DateOnly.FromDateTime(EF.Functions.AtTimeZone(c.Intervalo.Inicio, fuso).Date) < v.Ate)))));
    }

    /// <summary>Nome do fuso para o AT TIME ZONE do SQL Server, que só conhece ids do Windows.</summary>
    private static string FusoSql(TimeZoneInfo fuso) =>
        TimeZoneInfo.TryConvertIanaIdToWindowsId(fuso.Id, out var windows) ? windows : fuso.Id;
}
