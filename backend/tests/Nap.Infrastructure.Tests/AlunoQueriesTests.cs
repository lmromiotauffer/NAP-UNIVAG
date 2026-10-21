using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Domain.Alunos;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Encaminhamentos;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Tests;

public class AlunoQueriesTests(SqlServerFixture fx)
{
    [Fact]
    public async Task Listagem_de_alunos_aplica_escopo_antes_da_paginacao()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var cursoA = await fx.CriarCursoAsync("Curso A");
        var cursoB = await fx.CriarCursoAsync("Curso B");
        for (var i = 0; i < 3; i++) await RoundTripTests.CriarAlunoAsync(fx, cursoA, prof, $"Aluno A{i}");
        for (var i = 0; i < 5; i++) await RoundTripTests.CriarAlunoAsync(fx, cursoB, prof, $"Aluno B{i}");

        var sql = new List<string>();
        await using var db = fx.CriarContexto(sql.Add);
        var resultado = await new AlunoQueries(db)
            .ListarAsync(new FiltroAlunos(null, null, prof, null, null, null, 1, 2), new EscopoAlunos(cursoA, false), ct);
        var equipeNap = await new AlunoQueries(db)
            .ListarAsync(new FiltroAlunos(null, cursoA, prof, null, null, null, 1, 10), new EscopoAlunos(null, true), ct);
        foreach (var s in sql) TestContext.Current.TestOutputHelper?.WriteLine(s);

        Assert.All(equipeNap.Itens, i => Assert.Equal((0, 0), (i.TotalAtendimentos, i.TotalFaltas)));
        Assert.Equal(3, resultado.Total);
        Assert.Equal(2, resultado.Itens.Count);
        Assert.All(resultado.Itens, i => Assert.Equal(cursoA, i.CursoId));
        Assert.All(resultado.Itens, i => Assert.Null(i.TotalAtendimentos));
        Assert.Equal(["Aluno A0", "Aluno A1"], resultado.Itens.Select(i => i.Nome));
    }

    private async Task<(Guid Curso, Guid PsicologiaId, Guid CoordenacaoId, Guid SemEncaminhamentoId)> SemearEncaminhamentosAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync($"Curso dos encaminhamentos {Guid.NewGuid():N}");
        var psicologia = await RoundTripTests.CriarAlunoAsync(fx, curso, prof, "Aluno com psicologia");
        var coordenacao = await RoundTripTests.CriarAlunoAsync(fx, curso, prof, "Aluno com coordenação");
        var sem = await RoundTripTests.CriarAlunoAsync(fx, curso, prof, "Aluno sem encaminhamento");
        var hoje = new DateOnly(2026, 10, 3);
        var agora = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        await using var db = fx.CriarContexto();
        db.Encaminhamentos.Add(Encaminhamento.Criar(psicologia.Id, curso, prof, Especialidade.Psicologia, DestinoEncaminhamento.Sip,
            hoje, "motivo", "resumo", null, hoje, agora));
        db.Encaminhamentos.Add(Encaminhamento.Criar(coordenacao.Id, curso, prof, Especialidade.CoordenacaoCurso,
            DestinoEncaminhamento.CoordenacaoCurso, hoje, "motivo", "resumo", null, hoje, agora));
        await db.SaveChangesAsync(ct);
        return (curso, psicologia.Id, coordenacao.Id, sem.Id);
    }

    private static FiltroAlunos PorEspecialidade(Guid curso, Especialidade? especialidade) =>
        new(null, curso, null, null, null, null, 1, 50, especialidade);

    [Fact]
    public async Task Filtra_alunos_pela_especialidade_do_encaminhamento()
    {
        var ct = TestContext.Current.CancellationToken;
        var (curso, psicologia, coordenacao, _) = await SemearEncaminhamentosAsync();
        await using var db = fx.CriarContexto();
        var consultas = new AlunoQueries(db);
        var equipe = new EscopoAlunos(null, true);

        var porPsicologia = await consultas.ListarAsync(PorEspecialidade(curso, Especialidade.Psicologia), equipe, ct);
        var porCoordenacao = await consultas.ListarAsync(PorEspecialidade(curso, Especialidade.CoordenacaoCurso), equipe, ct);
        var porPsiquiatria = await consultas.ListarAsync(PorEspecialidade(curso, Especialidade.Psiquiatria), equipe, ct);

        Assert.Equal([psicologia], porPsicologia.Itens.Select(i => i.Id));
        Assert.Equal([coordenacao], porCoordenacao.Itens.Select(i => i.Id));
        Assert.Empty(porPsiquiatria.Itens);
    }

    [Fact]
    public async Task Coordenador_de_curso_so_casa_destino_coordenacao_de_curso()
    {
        var ct = TestContext.Current.CancellationToken;
        var (curso, _, coordenacao, _) = await SemearEncaminhamentosAsync();
        await using var db = fx.CriarContexto();
        var consultas = new AlunoQueries(db);
        var escopo = new EscopoAlunos(curso, false);

        // Encaminhamento clínico (Psicologia → SIP) do aluno do curso não pode ser revelado por esse filtro.
        Assert.Empty((await consultas.ListarAsync(PorEspecialidade(curso, Especialidade.Psicologia), escopo, ct)).Itens);
        Assert.Equal([coordenacao],
            (await consultas.ListarAsync(PorEspecialidade(curso, Especialidade.CoordenacaoCurso), escopo, ct)).Itens.Select(i => i.Id));
    }

    [Fact]
    public async Task Sem_filtro_mantem_a_lista_atual()
    {
        var ct = TestContext.Current.CancellationToken;
        var (curso, _, _, _) = await SemearEncaminhamentosAsync();
        await using var db = fx.CriarContexto();

        var lista = await new AlunoQueries(db).ListarAsync(PorEspecialidade(curso, null), new EscopoAlunos(null, true), ct);

        Assert.Equal(3, lista.Total);
    }

    [Fact]
    public async Task Linha_do_tempo_intercala_fontes_e_pagina_por_data()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: "Ana Profissional");
        var curso = await fx.CriarCursoAsync();
        var cursoNovo = await fx.CriarCursoAsync("Curso novo da linha do tempo");
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        var agora = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        await using (var db = fx.CriarContexto())
        {
            var a = (await new Persistencia.Repositorios.AlunoRepository(db).ObterAsync(aluno.Id, ct))!;
            a.AlterarSituacao(SituacaoAluno.Ativo, "Motivo qualquer", prof, agora);
            a.TransferirCurso(cursoNovo, new DateOnly(2026, 10, 1), "Mudança de curso", prof, agora.AddHours(-3));
            db.Atendimentos.Add(Atendimento.RegistrarAvulso(aluno.Id, prof, IntervaloHorario.Criar(agora.AddHours(-2), agora.AddHours(-1)),
                TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, new DadosClinicos("Demanda", "Segredo", null, null),
                "Justificativa avulso", prof, agora));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var consultas = new AlunoQueries(leitura);
        var p1 = (await consultas.LinhaDoTempoAsync(aluno.Id, new EscopoAlunos(null, true), 1, 2, ct))!;
        var p2 = (await consultas.LinhaDoTempoAsync(aluno.Id, new EscopoAlunos(null, true), 2, 2, ct))!;
        var p3 = (await consultas.LinhaDoTempoAsync(aluno.Id, new EscopoAlunos(null, true), 3, 2, ct))!;
        var semClinico = (await consultas.LinhaDoTempoAsync(aluno.Id, new EscopoAlunos(cursoNovo, false), 1, 10, ct))!;
        var todos = p1.Itens.Concat(p2.Itens).Concat(p3.Itens).ToList();

        // 2 mudanças de situação + 2 vínculos de curso + 1 atendimento, do mais recente ao mais antigo; os dois
        // registros do cadastro têm o mesmo instante (ordem entre eles decidida pelo Id).
        Assert.Equal(5, p1.Total);
        Assert.Equal(["situacao", "atendimento", "curso"], todos.Take(3).Select(e => e.Tipo));
        Assert.Equal(["curso", "situacao"], todos.Skip(3).Select(e => e.Tipo).Order());
        Assert.Equal("Ana Profissional", todos[1].Autor);
        Assert.Equal(("Ana Profissional", "Mudança de curso"), (todos[2].Autor, todos[2].Resumo));
        Assert.Contains(todos, e => e.Tipo == "curso" && e.Resumo == "Cadastro inicial" && e.Autor == "Ana Profissional");
        Assert.DoesNotContain(todos, e => (e.Resumo ?? "").Contains("Segredo"));
        // Coordenador do curso novo: só o que veio a partir da transferência (vínculo novo + situação posterior).
        Assert.Equal(2, semClinico.Total);
        Assert.DoesNotContain(semClinico.Itens, e => e.Tipo == "atendimento");
        Assert.Null(await consultas.LinhaDoTempoAsync(aluno.Id, new EscopoAlunos(Guid.NewGuid(), false), 1, 10, ct));
    }

    [Fact]
    public async Task Coordenador_de_curso_nao_ve_motivo_nem_historico_anterior_a_transferencia()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var cursoAntigo = await fx.CriarCursoAsync("Curso antigo");
        var cursoNovo = await fx.CriarCursoAsync("Curso novo da transferência");
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, cursoAntigo, prof);
        var agora = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        await using (var db = fx.CriarContexto())
        {
            var a = (await new Persistencia.Repositorios.AlunoRepository(db).ObterAsync(aluno.Id, ct))!;
            a.AlterarSituacao(SituacaoAluno.Ativo, "Motivo do curso antigo", prof, agora.AddDays(-2));
            a.TransferirCurso(cursoNovo, new DateOnly(2026, 10, 4), "Motivo da transferência", prof, agora.AddDays(-1));
            a.AlterarSituacao(SituacaoAluno.EmAcompanhamento, "Motivo sensível", prof, agora);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var consultas = new AlunoQueries(leitura);
        var coordenador = new EscopoAlunos(cursoNovo, false);
        var paginas = new List<PagedResult<EventoLinhaDoTempoDto>>();
        for (var p = 1; p <= 3; p++) paginas.Add((await consultas.LinhaDoTempoAsync(aluno.Id, coordenador, p, 1, ct))!);
        var vistos = paginas.SelectMany(p => p.Itens).ToList();

        // Contagem e paginação sob o mesmo corte: 2 eventos, 1 por página, a 3ª vazia.
        Assert.All(paginas, p => Assert.Equal(2, p.Total));
        Assert.Equal(["situacao", "curso"], vistos.Select(e => e.Tipo));
        Assert.Empty(paginas[2].Itens);
        Assert.All(vistos, e => Assert.Null(e.Resumo));
        Assert.Contains("Curso novo", vistos[1].Titulo);

        var equipe = (await consultas.LinhaDoTempoAsync(aluno.Id, new EscopoAlunos(null, true), 1, 20, ct))!;
        Assert.Equal(5, equipe.Total);
        Assert.Contains(equipe.Itens, e => e.Resumo == "Motivo do curso antigo");
    }

    [Fact]
    public async Task Coordenador_do_curso_anterior_alcanca_a_ficha_e_so_a_linha_do_tempo_do_seu_periodo()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var cursoAntigo = await fx.CriarCursoAsync("Curso antigo do coordenador");
        var cursoNovo = await fx.CriarCursoAsync("Curso novo depois da transferência");
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, cursoAntigo, prof);
        var agora = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        await using (var db = fx.CriarContexto())
        {
            var a = (await new Persistencia.Repositorios.AlunoRepository(db).ObterAsync(aluno.Id, ct))!;
            a.AlterarSituacao(SituacaoAluno.Ativo, "Motivo do curso antigo", prof, agora.AddDays(-2));
            a.TransferirCurso(cursoNovo, new DateOnly(2026, 10, 4), "Motivo da transferência", prof, agora.AddDays(-1));
            a.AlterarSituacao(SituacaoAluno.EmAcompanhamento, "Motivo do curso novo", prof, agora);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var consultas = new AlunoQueries(leitura);
        var antigo = new EscopoAlunos(cursoAntigo, false);

        // A ficha segue alcançável (o link do encaminhamento gravado com o curso antigo não morre), sem o resumo clínico.
        var ficha = await consultas.ObterFichaAsync(aluno.Id, antigo, ct);
        Assert.NotNull(ficha);
        Assert.Null(ficha.Resumo);
        Assert.Contains((await consultas.ListarAsync(new FiltroAlunos(null, null, null, null, null, null, 1, 50), antigo, ct)).Itens,
            i => i.Id == aluno.Id);

        // Linha do tempo: só o período dele, até a transferência (o vínculo seguinte já é período de outro coordenador).
        var linha = (await consultas.LinhaDoTempoAsync(aluno.Id, antigo, 1, 20, ct))!;
        Assert.Equal(linha.Itens.Count, linha.Total);
        Assert.NotEmpty(linha.Itens);
        Assert.All(linha.Itens, e => Assert.True(e.Em < agora.AddDays(-1)));
        Assert.DoesNotContain(linha.Itens, e => e.Titulo.Contains("Curso novo"));
        Assert.All(linha.Itens, e => Assert.Null(e.Resumo));

        // O coordenador do curso atual continua vendo só a partir da transferência.
        var novo = (await consultas.LinhaDoTempoAsync(aluno.Id, new EscopoAlunos(cursoNovo, false), 1, 20, ct))!;
        Assert.All(novo.Itens, e => Assert.True(e.Em >= agora.AddDays(-1)));
    }

    [Fact]
    public async Task Linha_do_tempo_inclui_encaminhamento_e_evolucoes_para_equipe()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: "Solicitante NAP");
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        var agora = EncaminhamentoQueriesTests.Agora;
        await EncaminhamentoQueriesTests.SalvarAsync(fx, aluno.Id, curso, prof, DestinoEncaminhamento.Sip, alterar: e =>
        {
            e.RegistrarRetorno(AcaoRetorno.Acolhimento, EncaminhamentoQueriesTests.Hoje, "Acolhido no SIP", prof, EncaminhamentoQueriesTests.Hoje, agora.AddMinutes(1));
            e.RegistrarRetorno(AcaoRetorno.Evolucao, null, "Evolução sensível", prof, EncaminhamentoQueriesTests.Hoje, agora.AddMinutes(2));
        });

        var sql = new List<string>();
        await using var leitura = fx.CriarContexto(sql.Add);
        var consultas = new AlunoQueries(leitura);
        var p1 = (await consultas.LinhaDoTempoAsync(aluno.Id, new EscopoAlunos(null, true), 1, 2, ct))!;
        var tudo = (await consultas.LinhaDoTempoAsync(aluno.Id, new EscopoAlunos(null, true), 1, 20, ct))!;
        foreach (var s in sql) TestContext.Current.TestOutputHelper?.WriteLine(s);

        // Cadastro (situação + curso) + criação + 2 evoluções.
        Assert.Equal(5, p1.Total);
        Assert.Equal(["Encaminhamento: Evolução", "Encaminhamento: Acolhimento"], p1.Itens.Select(e => e.Titulo));
        var encaminhamentos = tudo.Itens.Where(e => e.Tipo == "encaminhamento").ToList();
        Assert.Equal(3, encaminhamentos.Count);
        Assert.Equal(("Encaminhamento para Psicologia · SIP", "Resumo compartilhável", "Solicitante NAP"),
            (encaminhamentos[2].Titulo, encaminhamentos[2].Resumo, encaminhamentos[2].Autor));
        Assert.Equal(["Evolução sensível", "Acolhido no SIP"], encaminhamentos.Take(2).Select(e => e.Resumo));
        Assert.DoesNotContain(tudo.Itens, e => (e.Resumo ?? "").Contains("Motivo restrito"));
    }

    [Fact]
    public async Task Linha_do_tempo_do_coordenador_mostra_resumo_so_do_encaminhamento_a_coordenacao()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var outroCurso = await fx.CriarCursoAsync("Outro curso");
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        await EncaminhamentoQueriesTests.SalvarAsync(fx, aluno.Id, curso, prof, DestinoEncaminhamento.CoordenacaoCurso, alterar: e =>
            e.RegistrarRetorno(AcaoRetorno.Evolucao, null, "Texto da evolução", prof, EncaminhamentoQueriesTests.Hoje,
                EncaminhamentoQueriesTests.Agora.AddMinutes(1)));
        // Gravado com outro curso: fora do recorte do coordenador.
        await EncaminhamentoQueriesTests.SalvarAsync(fx, aluno.Id, outroCurso, prof, DestinoEncaminhamento.Sip);

        await using var leitura = fx.CriarContexto();
        var consultas = new AlunoQueries(leitura);
        var coordenador = (await consultas.LinhaDoTempoAsync(aluno.Id, new EscopoAlunos(curso, false), 1, 20, ct))!;
        var equipe = (await consultas.LinhaDoTempoAsync(aluno.Id, new EscopoAlunos(null, true), 1, 20, ct))!;

        // Cadastro (situação + curso) + criação + 1 evolução do curso dele.
        Assert.Equal(4, coordenador.Total);
        Assert.Equal(["Encaminhamento: Evolução", "Encaminhamento para Coordenação do curso · Coordenação do curso"],
            coordenador.Itens.Where(e => e.Tipo == "encaminhamento").Select(e => e.Titulo));
        // Mesma regra do detalhe do encaminhamento: resumo e texto da evolução quando o destino é a coordenação dele.
        Assert.Equal(["Texto da evolução", "Resumo compartilhável"],
            coordenador.Itens.Where(e => e.Tipo == "encaminhamento").Select(e => e.Resumo));
        Assert.All(coordenador.Itens.Where(e => e.Tipo != "encaminhamento"), e => Assert.Null(e.Resumo));
        Assert.Equal(5, equipe.Total);
    }

    [Fact]
    public async Task Linha_do_tempo_do_coordenador_nao_revela_especialidade_nem_destino_clinico()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        await EncaminhamentoQueriesTests.SalvarAsync(fx, aluno.Id, curso, prof, DestinoEncaminhamento.Sip, alterar: e =>
            e.RegistrarRetorno(AcaoRetorno.Evolucao, null, "Texto do SIP", prof, EncaminhamentoQueriesTests.Hoje,
                EncaminhamentoQueriesTests.Agora.AddMinutes(1)));

        await using var leitura = fx.CriarContexto();
        var coordenador = (await new AlunoQueries(leitura).LinhaDoTempoAsync(aluno.Id, new EscopoAlunos(curso, false), 1, 20, ct))!;

        Assert.NotNull(coordenador);
        Assert.Equal(["Encaminhamento: Evolução", "Encaminhamento"],
            coordenador.Itens.Where(e => e.Tipo == "encaminhamento").Select(e => e.Titulo));
        // Destino clínico (SIP): resumo e texto da evolução ficam nulos para o coordenador.
        Assert.All(coordenador.Itens, e => Assert.Null(e.Resumo));
    }

    [Fact]
    public async Task Linha_do_tempo_fora_do_escopo_e_nula()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var outroCurso = await fx.CriarCursoAsync("Curso de outro coordenador");
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);

        await using var leitura = fx.CriarContexto();
        Assert.Null(await new AlunoQueries(leitura).LinhaDoTempoAsync(aluno.Id, new EscopoAlunos(outroCurso, false), 1, 20, ct));
        Assert.Null(await new AlunoQueries(leitura).LinhaDoTempoAsync(Guid.NewGuid(), new EscopoAlunos(null, true), 1, 20, ct));
    }

    [Fact]
    public async Task Faltas_do_aluno_contam_so_atendimentos_individuais()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        var inicio = new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.FromHours(-4));
        await using (var db = fx.CriarContexto())
        {
            foreach (var (tipo, h) in new[] { (TipoAtendimento.Acompanhamento, 0), (TipoAtendimento.Reuniao, 2) })
                db.Atendimentos.Add(Atendimento.RegistrarAvulso(aluno.Id, prof, IntervaloHorario.Criar(inicio.AddHours(h), inicio.AddHours(h + 1)),
                    tipo, EstadoAtendimento.Ausente, null, "Justificativa avulso", prof, inicio.AddDays(1)));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var consultas = new AlunoQueries(leitura);
        var lista = await consultas.ListarAsync(new FiltroAlunos(null, curso, prof, null, null, null, 1, 10), new EscopoAlunos(null, true), ct);
        var ficha = await consultas.ObterFichaAsync(aluno.Id, new EscopoAlunos(null, true), ct);

        Assert.Equal(1, Assert.Single(lista.Itens).TotalFaltas);
        Assert.Equal(1, ficha!.Resumo!.TotalFaltas);
    }
}
