using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Painel;
using Nap.Application.Features.Peis;
using Nap.Domain.Agenda;
using Nap.Domain.Alunos;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Encaminhamentos;
using Nap.Domain.Peis;
using Nap.Infrastructure.Identidade;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Tests;

public class PainelQueriesTests(SqlServerFixture fx)
{
    // Horários locais de Cuiabá (UTC-4).
    private static DateTimeOffset Local(int mes, int dia, int hora) => new(2026, mes, dia, hora, 0, 0, TimeSpan.FromHours(-4));

    [Fact]
    public async Task Painel_conta_reuniao_passada_sem_desfecho()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional reunião sem desfecho");
        var agora = Local(9, 30, 12);
        Compromisso Passado(TipoCompromisso tipo, int hora) => Compromisso.Criar(tipo, "Sem desfecho", IntervaloHorario.Criar(Local(9, 22, hora), Local(9, 22, hora + 1)),
            prof, null, null, prof);
        var realizada = Passado(TipoCompromisso.ElaboracaoPei, 11);
        realizada.RegistrarRealizacao(IntervaloHorario.Criar(Local(9, 22, 11), Local(9, 22, 12)), prof, agora);
        await using (var db = fx.CriarContexto())
        {
            db.AddRange(Passado(TipoCompromisso.ReuniaoEquipe, 8), Passado(TipoCompromisso.TarefaAdministrativa, 9),
                Passado(TipoCompromisso.AtividadeCurso, 10), Passado(TipoCompromisso.ReuniaoCoordenacaoGestao, 15),
                Passado(TipoCompromisso.EncaminhamentoDevolutiva, 16), Passado(TipoCompromisso.Lembrete, 13), Passado(TipoCompromisso.Bloqueio, 14), realizada);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var semana = await new PainelQueries(leitura, SqlServerFixture.Relogio()).ObterSemanaAsync(new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 25), prof, null, ct);
        Assert.Equal(5, semana.CompromissosSemDesfecho);
    }

    [Fact]
    public async Task Alerta_de_faltas_na_janela_conta_so_a_partir_do_primeiro_dia_da_janela()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional da janela de faltas");
        var curso = await fx.CriarCursoAsync("Curso da janela");
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof, "Aluno da janela");
        var agora = Local(12, 1, 12);
        Atendimento Ausente(DateTimeOffset inicio) =>
            Atendimento.RegistrarAvulso(aluno.Id, prof, IntervaloHorario.Criar(inicio, inicio.AddMinutes(50)), TipoAtendimento.Acompanhamento,
                EstadoAtendimento.Ausente, null, "Justificativa avulso", prof, agora);
        await using (var db = fx.CriarContexto())
        {
            // Janela de 30 dias a partir de hoje = 01/10: começa em 02/09 (inclusive); 01/09 fica fora.
            db.AddRange(Ausente(Local(9, 1, 10)), Ausente(Local(9, 2, 10)));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var painel = new PainelQueries(leitura, SqlServerFixture.Relogio());
        var hoje = new DateOnly(2026, 10, 1);
        // FaltasConsecutivas = 10 nunca dispara com 2 registros: só a janela decide.
        var umaBasta = await painel.ObterAlertasAsync(hoje, prof, null, SqlServerFixture.AlertasPadrao with { FaltasConsecutivas = 10, FaltasJanela = 1 }, ct);
        var duasNaJanela = await painel.ObterAlertasAsync(hoje, prof, null, SqlServerFixture.AlertasPadrao with { FaltasConsecutivas = 10, FaltasJanela = 2 }, ct);

        var alerta = Assert.Single(umaBasta, a => a.Tipo == "faltas_recorrentes");
        Assert.Equal((aluno.Id, 1), (alerta.AlunoId!.Value, alerta.DiasOuOcorrencias!.Value));
        Assert.Contains("nos últimos 30 dias", alerta.Detalhe);
        Assert.DoesNotContain(duasNaJanela, a => a.Tipo == "faltas_recorrentes");

        // Com alta, as faltas antigas não alertam mais.
        await using (var db = fx.CriarContexto())
        {
            var comAlta = await db.Alunos.SingleAsync(a => a.Id == aluno.Id, ct);
            comAlta.AlterarSituacao(SituacaoAluno.Alta, "Concluiu o acompanhamento", prof, agora);
            await db.SaveChangesAsync(ct);
        }
        await using var depois = fx.CriarContexto();
        var aposAlta = await new PainelQueries(depois, SqlServerFixture.Relogio())
            .ObterAlertasAsync(hoje, prof, null, SqlServerFixture.AlertasPadrao with { FaltasConsecutivas = 10, FaltasJanela = 1 }, ct);
        Assert.DoesNotContain(aposAlta, a => a.Tipo == "faltas_recorrentes");
    }

    [Fact]
    public async Task Alertas_de_faltas_param_no_teto_com_um_resumo()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional com muitas faltas");
        var curso = await fx.CriarCursoAsync("Curso com muitas faltas");
        var agora = Local(12, 1, 12);
        await using (var db = fx.CriarContexto())
        {
            for (var i = 0; i < 52; i++)
            {
                var aluno = Aluno.Criar($"Aluno faltoso {i:00}", Matricula.Criar(Guid.NewGuid().ToString("N")[..20]), curso, 3,
                    new DateOnly(2026, 3, 1), prof, null, null, prof, agora);
                db.Add(aluno);
                db.Add(Atendimento.RegistrarAvulso(aluno.Id, prof, IntervaloHorario.Criar(Local(9, 20, 10), Local(9, 20, 11)),
                    TipoAtendimento.Acompanhamento, EstadoAtendimento.Ausente, null, "Justificativa avulso", prof, agora));
            }
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var alertas = await new PainelQueries(leitura, SqlServerFixture.Relogio()).ObterAlertasAsync(new DateOnly(2026, 10, 1), prof, null,
            SqlServerFixture.AlertasPadrao with { FaltasConsecutivas = 10, FaltasJanela = 1 }, ct);

        var faltas = alertas.Where(a => a.Tipo == "faltas_recorrentes").ToList();
        Assert.Equal(51, faltas.Count);
        Assert.Equal(50, faltas.Count(a => a.AlunoId is not null));
        var resumo = Assert.Single(faltas, a => a.AlunoId is null);
        Assert.Equal(2, resumo.DiasOuOcorrencias);
    }

    [Fact]
    public async Task Painel_reparte_minutos_que_viram_a_semana_e_o_mes_como_a_producao()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional da virada");
        var curso = await fx.CriarCursoAsync("Curso da virada");
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof, "Aluno da virada");
        var agora = Local(12, 1, 12);
        Atendimento Realizado(DateTimeOffset inicio) => Atendimento.RegistrarAvulso(aluno.Id, prof,
            IntervaloHorario.Criar(inicio, inicio.AddHours(2)), TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado,
            new DadosClinicos("Demanda", null, null, null), "Justificativa avulso", prof, agora);

        await using (var db = fx.CriarContexto())
        {
            // Domingo 27/09 23h → segunda 28/09 1h; quarta 30/09 23h → quinta 01/10 1h.
            db.AddRange(Realizado(Local(9, 27, 23)), Realizado(Local(9, 30, 23)));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var painel = new PainelQueries(leitura, SqlServerFixture.Relogio());
        var semana = await painel.ObterSemanaAsync(new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 2), prof, null, ct);
        var serie = await painel.ObterSerieMensalAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), prof, null, ct);

        // A semana conta só o de quarta (início nela), mas soma a hora de segunda do de domingo.
        Assert.Equal((1, 60 + 120), (semana.Realizados, semana.Minutos));
        // Setembro: 120 do de domingo + 60 do de quarta; outubro só soma a hora que virou o mês.
        Assert.Equal([new SerieMensalDto(2026, 9, 2, 180, false), new SerieMensalDto(2026, 10, 0, 60, false)], serie);
    }

    [Fact]
    public async Task Painel_por_profissional_fecha_com_o_total_da_semana_na_virada_do_dia()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional da virada por profissional");
        var curso = await fx.CriarCursoAsync("Curso da virada por profissional");
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof, "Aluno da virada por profissional");
        var agora = Local(12, 1, 12);
        Atendimento Realizado(DateTimeOffset inicio) => Atendimento.RegistrarAvulso(aluno.Id, prof,
            IntervaloHorario.Criar(inicio, inicio.AddHours(2)), TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado,
            new DadosClinicos("Demanda", null, null, null), "Justificativa avulso", prof, agora);

        await using (var db = fx.CriarContexto())
        {
            // Domingo 27/09 23h → segunda 28/09 1h (só a hora de segunda é da semana); quarta 30/09 23h → quinta 01/10 1h.
            db.AddRange(Realizado(Local(9, 27, 23)), Realizado(Local(9, 30, 23)));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var painel = new PainelQueries(leitura, SqlServerFixture.Relogio());
        var semana = await painel.ObterSemanaAsync(new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 2), prof, null, ct);
        var porProfissional = (await painel.ObterPorProfissionalAsync(new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 2), null, ct))
            .Single(p => p.Id == prof);

        Assert.Equal((1, 180), (semana.Realizados, semana.Minutos));
        Assert.Equal((semana.Realizados, semana.Faltas, semana.Minutos), (porProfissional.Realizados, porProfissional.Faltas, porProfissional.Minutos));
    }

    [Fact]
    public async Task Painel_nao_conta_ausencia_como_realizado_e_omite_mes_futuro()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional do painel");
        var curso = await fx.CriarCursoAsync("Curso do painel");
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof, "Aluno do painel");
        var agora = Local(12, 1, 12);

        Atendimento Avulso(EstadoAtendimento estado, TipoAtendimento tipo, DateTimeOffset inicio, int minutos) =>
            Atendimento.RegistrarAvulso(aluno.Id, prof, IntervaloHorario.Criar(inicio, inicio.AddMinutes(minutos)), tipo, estado,
                estado == EstadoAtendimento.Realizado ? new DadosClinicos("Demanda", null, null, null) : null, "Justificativa avulso", prof, agora);

        await using (var db = fx.CriarContexto())
        {
            db.AddRange(
                Avulso(EstadoAtendimento.Realizado, TipoAtendimento.Acompanhamento, Local(9, 21, 10), 50),
                Avulso(EstadoAtendimento.Ausente, TipoAtendimento.Acompanhamento, Local(9, 22, 10), 60),
                Avulso(EstadoAtendimento.Ausente, TipoAtendimento.Psicopedagogico, Local(9, 23, 10), 60),
                Avulso(EstadoAtendimento.Ausente, TipoAtendimento.Devolutiva, Local(9, 24, 10), 60),
                Avulso(EstadoAtendimento.Ausente, TipoAtendimento.Reuniao, Local(9, 24, 14), 60),
                // 31/08 22h local = 01/09 02h UTC: pertence a agosto.
                Avulso(EstadoAtendimento.Realizado, TipoAtendimento.Acompanhamento, Local(8, 31, 22), 60),
                Avulso(EstadoAtendimento.Realizado, TipoAtendimento.Acompanhamento, Local(11, 10, 10), 60),
                Compromisso.Criar(TipoCompromisso.Atendimento, "Sem desfecho", IntervaloHorario.Criar(Local(9, 25, 8), Local(9, 25, 9)),
                    prof, aluno.Id, null, prof));
            await db.SaveChangesAsync(ct);
        }

        var sql = new List<string>();
        await using var leitura = fx.CriarContexto(sql.Add);
        var painel = new PainelQueries(leitura, SqlServerFixture.Relogio());
        var inicio = new DateOnly(2026, 9, 21);
        var fim = new DateOnly(2026, 9, 25);

        var semana = await painel.ObterSemanaAsync(inicio, fim, prof, null, ct);
        var serie = await painel.ObterSerieMensalAsync(new DateOnly(2026, 6, 1), new DateOnly(2026, 10, 1), prof, null, ct);
        // Janela inalcançável: o alerta só pode vir da sequência; o Realizado de novembro (futuro em relação a "hoje")
        // não pode quebrar as 3 faltas consecutivas.
        var alertas = await painel.ObterAlertasAsync(fim, prof, null, SqlServerFixture.AlertasPadrao with { FaltasJanela = 10 }, ct);
        var porProfissional = await painel.ObterPorProfissionalAsync(inicio, fim, curso, ct);
        var agendaDoDia = await painel.ObterAgendaDoDiaAsync(fim, prof, ct);
        var porCurso = await painel.ContarAlunosPorCursoAsync(curso, ct);
        var porSituacao = await painel.ContarAlunosPorSituacaoAsync(curso, ct);
        foreach (var s in sql) TestContext.Current.TestOutputHelper?.WriteLine(s);

        Assert.Equal(new SemanaBrutaDto(Realizados: 1, Minutos: 50, Individuais: 1, Faltas: 3, CompromissosSemDesfecho: 1), semana);
        Assert.Equal([new SerieMensalDto(2026, 8, 1, 60, false), new SerieMensalDto(2026, 9, 1, 50, false)], serie);
        var faltas = Assert.Single(alertas, a => a.Tipo == "faltas_recorrentes");
        Assert.Equal((aluno.Id, 3), (faltas.AlunoId!.Value, faltas.DiasOuOcorrencias!.Value));
        Assert.Contains("consecutivas", faltas.Detalhe);
        Assert.Equal(1, Assert.Single(alertas, a => a.Tipo == "sem_desfecho").DiasOuOcorrencias);
        Assert.Equal([new ProducaoProfissionalDto(prof, "Profissional do painel", 1, 3, 50)], porProfissional);
        Assert.Equal("Aluno do painel", Assert.Single(agendaDoDia).Aluno);
        Assert.Equal([new ContagemDto(curso.ToString(), "Curso do painel", 1)], porCurso);
        Assert.Equal(1, Assert.Single(porSituacao).Total);
    }

    [Fact]
    public async Task Painel_conta_encaminhamentos_abertos_por_escopo_e_gera_alertas_sem_motivo()
    {
        var ct = TestContext.Current.CancellationToken;
        var hoje = new DateOnly(2026, 10, 20);
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync("Curso sem coordenador");
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof, "Aluno dos encaminhamentos");

        static DateTimeOffset Em(DateOnly d) => new(d.Year, d.Month, d.Day, 12, 0, 0, TimeSpan.FromHours(-4));
        Encaminhamento Novo(DestinoEncaminhamento destino, DateOnly data, params (AcaoRetorno Acao, DateOnly Data)[] retornos)
        {
            var e = Encaminhamento.Criar(aluno.Id, curso, prof,
                destino == DestinoEncaminhamento.CoordenacaoCurso ? Especialidade.CoordenacaoCurso : Especialidade.Psicologia,
                destino, data, "Motivo restrito", "Resumo compartilhável", null, data, Em(data));
            foreach (var r in retornos) e.RegistrarRetorno(r.Acao, r.Data, null, prof, r.Data, Em(r.Data));
            return e;
        }

        await using (var db = fx.CriarContexto())
        {
            db.AddRange(
                // Aguardando há 10 dias: sem acolhimento no prazo, mas atualizado há menos de 15.
                Novo(DestinoEncaminhamento.Sip, hoje.AddDays(-10)),
                // Em atendimento, última movimentação há 17 dias: sem atualização.
                Novo(DestinoEncaminhamento.Externo, hoje.AddDays(-19), (AcaoRetorno.Acolhimento, hoje.AddDays(-18)), (AcaoRetorno.InicioAtendimento, hoje.AddDays(-17))),
                Novo(DestinoEncaminhamento.CoordenacaoCurso, hoje.AddDays(-1)),
                // Encerrado não conta.
                Novo(DestinoEncaminhamento.Sip, hoje.AddDays(-30), (AcaoRetorno.Acolhimento, hoje.AddDays(-29)),
                    (AcaoRetorno.InicioAtendimento, hoje.AddDays(-28)), (AcaoRetorno.Alta, hoje.AddDays(-27))));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var painel = new PainelQueries(leitura, SqlServerFixture.Relogio());
        var opcoes = SqlServerFixture.AlertasPadrao;
        EscopoEncaminhamentos Escopo(string perfil, Guid? cursoId = null) =>
            EscopoEncaminhamentos.Para(new UsuarioAtualTeste(Guid.NewGuid(), perfil, cursoId));

        var equipe = await painel.ContarEncaminhamentosAsync(Escopo(Perfis.CoordenadorNap), prof, hoje, opcoes, ct);
        var sip = await painel.ContarEncaminhamentosAsync(Escopo(Perfis.Sip), prof, hoje, opcoes, ct);
        var coordenador = await painel.ContarEncaminhamentosAsync(Escopo(Perfis.CoordenadorCurso, curso), null, hoje, opcoes, ct);
        var outroCurso = await painel.ContarEncaminhamentosAsync(Escopo(Perfis.CoordenadorCurso, Guid.NewGuid()), null, hoje, opcoes, ct);
        var outroSolicitante = await painel.ContarEncaminhamentosAsync(Escopo(Perfis.CoordenadorNap), Guid.NewGuid(), hoje, opcoes, ct);
        var alertas = await painel.ObterAlertasEncaminhamentoAsync(Escopo(Perfis.CoordenadorNap), prof, true, hoje, opcoes, ct);
        var alertasProfissional = await painel.ObterAlertasEncaminhamentoAsync(Escopo(Perfis.ProfissionalNap), prof, false, hoje, opcoes, ct);

        Assert.Equal(new EncaminhamentosPainelDto(3, 2, 1, 1, 1, 15), equipe);
        Assert.Equal(new EncaminhamentosPainelDto(1, 1, 0, 0, 1, 15), sip);
        Assert.Equal(equipe, coordenador);
        Assert.Equal(new EncaminhamentosPainelDto(0, 0, 0, 0, 0, 15), outroCurso);
        Assert.Equal(new EncaminhamentosPainelDto(0, 0, 0, 0, 0, 15), outroSolicitante);

        var semAcolhimento = Assert.Single(alertas, a => a.Tipo == "encaminhamento-sem-acolhimento");
        Assert.Equal((aluno.Id, 10), (semAcolhimento.AlunoId!.Value, semAcolhimento.DiasOuOcorrencias!.Value));
        // Destino coordenação do curso, que não tem coordenador ativo.
        Assert.Contains(alertas, a => a.Tipo == "encaminhamento-sem-destinatario" && a.DiasOuOcorrencias == 1);
        Assert.DoesNotContain(alertas, a => a.Detalhe.Contains("Motivo restrito"));
        Assert.Equal(["encaminhamento-sem-acolhimento"], alertasProfissional.Select(a => a.Tipo));
    }

    [Fact]
    public async Task Painel_conta_peis_por_perfil()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        async Task<Guid> NovoAluno() => (await RoundTripTests.CriarAlunoAsync(fx, curso, prof)).Id;
        await PeiQueriesTests.SalvarPeiAsync(fx, await NovoAluno(), prof, curso, EstadoVersaoPei.Rascunho);
        await PeiQueriesTests.SalvarPeiAsync(fx, await NovoAluno(), prof, curso, EstadoVersaoPei.EmRevisao);
        await PeiQueriesTests.SalvarPeiAsync(fx, await NovoAluno(), prof, curso, EstadoVersaoPei.AguardandoAssinaturas);
        await PeiQueriesTests.SalvarPeiAsync(fx, await NovoAluno(), prof, curso, EstadoVersaoPei.Assinada);
        await PeiQueriesTests.SalvarPeiAsync(fx, await NovoAluno(), prof, curso, EstadoVersaoPei.Publicada);
        var comCiencia = await PeiQueriesTests.SalvarPeiAsync(fx, await NovoAluno(), prof, curso, EstadoVersaoPei.Publicada);
        var coordenadorId = await fx.CriarProfissionalAsync(Perfis.CoordenadorCurso);
        await using (var escrita = fx.CriarContexto())
        {
            var pei = await escrita.Peis.Include(p => p.Versoes).ThenInclude(v => v.Ciencias).SingleAsync(p => p.Id == comCiencia.Id, ct);
            pei.Versoes.Single().RegistrarCiencia(coordenadorId, new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(-4)));
            await escrita.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var painel = new PainelQueries(leitura, SqlServerFixture.Relogio());
        EscopoPeis Escopo(string perfil, Guid? cursoId = null) => EscopoPeis.Para(new UsuarioAtualTeste(Guid.NewGuid(), perfil, cursoId));

        // Equipe: versões do responsável (filtro) em qualquer estado; "sem ciência" só conta publicadas sem nenhuma ciência.
        Assert.Equal(new PeisPainelDto(2, 1, 1), await painel.ContarPeisAsync(Escopo(Perfis.CoordenadorNap), prof, null, ct));
        Assert.Equal(new PeisPainelDto(2, 1, 1), await painel.ContarPeisAsync(Escopo(Perfis.ProfissionalNap), prof, null, ct));
        Assert.Equal(new PeisPainelDto(0, 0, 0), await painel.ContarPeisAsync(Escopo(Perfis.ProfissionalNap), Guid.NewGuid(), null, ct));
        // Coordenador do curso: só publicadas do curso dele, independente de quem consulta.
        Assert.Equal(new PeisPainelDto(0, 0, 1), await painel.ContarPeisAsync(Escopo(Perfis.CoordenadorCurso, curso), null, null, ct));
        Assert.Equal(new PeisPainelDto(0, 0, 0), await painel.ContarPeisAsync(Escopo(Perfis.CoordenadorCurso, Guid.NewGuid()), null, null, ct));
        // Filtro de curso do painel: curso atual do aluno.
        Assert.Equal(new PeisPainelDto(2, 1, 1), await painel.ContarPeisAsync(Escopo(Perfis.CoordenadorNap), prof, curso, ct));
        Assert.Equal(new PeisPainelDto(0, 0, 0), await painel.ContarPeisAsync(Escopo(Perfis.CoordenadorNap), prof, Guid.NewGuid(), ct));
    }

    [Fact]
    public async Task Alerta_pei_pendente_apos_15_dias()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        var hoje = EncaminhamentoQueriesTests.Hoje;
        // Enviado há exatamente 15 dias: ainda no prazo.
        await PeiQueriesTests.SalvarPeiAsync(fx, aluno.Id, prof, curso, EstadoVersaoPei.EmRevisao,
            new DateTimeOffset(hoje.AddDays(-15).ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(-4)));
        var outro = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        await PeiQueriesTests.SalvarPeiAsync(fx, outro.Id, prof, curso, EstadoVersaoPei.AguardandoAssinaturas,
            new DateTimeOffset(hoje.AddDays(-16).ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(-4)));

        await using var db = fx.CriarContexto();
        var painel = new PainelQueries(db, SqlServerFixture.Relogio());
        var alertas = await painel.ObterAlertasPeiAsync(prof, null, false, hoje, SqlServerFixture.AlertasPadrao, ct);

        var alerta = Assert.Single(alertas);
        Assert.Equal(("pei-pendente", outro.Id, 16), (alerta.Tipo, alerta.AlunoId!.Value, alerta.DiasOuOcorrencias!.Value));
        Assert.Contains("aguardando assinaturas", alerta.Detalhe);
        Assert.DoesNotContain("sigilosa", alerta.Detalhe);
        // Outro responsável não vê os alertas deste.
        Assert.Empty(await painel.ObterAlertasPeiAsync(Guid.NewGuid(), null, false, hoje, SqlServerFixture.AlertasPadrao, ct));
        // Filtro de curso do painel: curso atual do aluno.
        Assert.Single(await painel.ObterAlertasPeiAsync(prof, curso, false, hoje, SqlServerFixture.AlertasPadrao, ct));
        Assert.Empty(await painel.ObterAlertasPeiAsync(prof, Guid.NewGuid(), false, hoje, SqlServerFixture.AlertasPadrao, ct));
    }

    [Fact]
    public async Task Alerta_pei_sem_coordenador()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        await PeiQueriesTests.SalvarPeiAsync(fx, aluno.Id, prof, curso, EstadoVersaoPei.Publicada);
        var comCoordenador = await fx.CriarCursoAsync();
        var aluno2 = await RoundTripTests.CriarAlunoAsync(fx, comCoordenador, prof);
        await PeiQueriesTests.SalvarPeiAsync(fx, aluno2.Id, prof, comCoordenador, EstadoVersaoPei.Publicada);
        var cursoInativo = await fx.CriarCursoAsync();
        var aluno3 = await RoundTripTests.CriarAlunoAsync(fx, cursoInativo, prof);
        await PeiQueriesTests.SalvarPeiAsync(fx, aluno3.Id, prof, cursoInativo, EstadoVersaoPei.Publicada);
        var coordInativoId = Guid.NewGuid();
        var coordId = Guid.NewGuid();
        await using (var escrita = fx.CriarContexto())
        {
            escrita.Users.Add(new UsuarioIdentidade { Id = coordId, UserName = coordId.ToString("N"), NomeCompleto = "Coord", Perfil = Perfis.CoordenadorCurso, CursoId = comCoordenador, Ativo = true });
            // Coordenador inativo não conta como destinatário.
            escrita.Users.Add(new UsuarioIdentidade { Id = coordInativoId, UserName = coordInativoId.ToString("N"), NomeCompleto = "Coord inativo", Perfil = Perfis.CoordenadorCurso, CursoId = cursoInativo, Ativo = false });
            await escrita.SaveChangesAsync(ct);
        }

        await using var db = fx.CriarContexto();
        var painel = new PainelQueries(db, SqlServerFixture.Relogio());
        var hoje = EncaminhamentoQueriesTests.Hoje;
        var comAviso = await painel.ObterAlertasPeiAsync(prof, null, true, hoje, SqlServerFixture.AlertasPadrao, ct);
        var semAviso = await painel.ObterAlertasPeiAsync(prof, null, false, hoje, SqlServerFixture.AlertasPadrao, ct);

        var alvos = comAviso.Where(a => a.Tipo == "pei-sem-coordenador").ToList();
        Assert.Equal(2, alvos.Count);
        Assert.Contains(alvos, a => a.AlunoId == aluno.Id);
        Assert.Contains(alvos, a => a.AlunoId == aluno3.Id);
        var alerta = alvos[0];
        Assert.DoesNotContain("sigilosa", alerta.Detalhe);
        Assert.Empty(semAviso);
        // Filtro de curso do painel também vale para o alerta sem coordenador.
        var soUmCurso = await painel.ObterAlertasPeiAsync(prof, curso, true, hoje, SqlServerFixture.AlertasPadrao, ct);
        Assert.Equal(aluno.Id, Assert.Single(soUmCurso, a => a.Tipo == "pei-sem-coordenador").AlunoId);
    }
}
