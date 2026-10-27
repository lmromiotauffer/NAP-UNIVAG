using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Encaminhamentos;
using Nap.Domain.Comum;
using Nap.Domain.Encaminhamentos;
using Nap.Infrastructure.Persistencia.Consultas;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class EncaminhamentoQueriesTests(SqlServerFixture fx)
{
    internal static readonly DateOnly Hoje = new(2026, 10, 20);
    // Depois do cadastro dos alunos de teste (RoundTripTests): o corte de vínculo do coordenador fica antes.
    internal static readonly DateTimeOffset Agora = new(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(-4));
    private const int Dias = 7;

    internal static async Task<Encaminhamento> SalvarAsync(SqlServerFixture fx, Guid alunoId, Guid cursoId, Guid solicitante,
        DestinoEncaminhamento destino, DateOnly? data = null, Action<Encaminhamento>? alterar = null)
    {
        var especialidade = destino == DestinoEncaminhamento.CoordenacaoCurso ? Especialidade.CoordenacaoCurso : Especialidade.Psicologia;
        var e = Encaminhamento.Criar(alunoId, cursoId, solicitante, especialidade, destino, data ?? Hoje.AddDays(-1),
            "Motivo restrito", "Resumo compartilhável", "Observações internas", Hoje, Agora);
        alterar?.Invoke(e);
        await using var db = fx.CriarContexto();
        db.Add(e);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return e;
    }

    private static EscopoEncaminhamentos Escopo(string perfil, Guid? cursoId = null) =>
        EscopoEncaminhamentos.Para(new UsuarioAtualTeste(Guid.NewGuid(), perfil, cursoId));

    private static FiltroEncaminhamentos Filtro(Guid? alunoId = null, bool? semAcolhimento = null, int pagina = 1, int tamanho = 20) =>
        new(alunoId, null, null, null, null, null, null, semAcolhimento, pagina, tamanho);

    private async Task<(Guid Prof, Guid Curso, Guid Aluno)> CenarioAsync()
    {
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        return (prof, curso, aluno.Id);
    }

    private async Task<(Guid Prof, Guid Curso, Guid Aluno)> CenarioComTodosOsDestinosAsync()
    {
        var c = await CenarioAsync();
        foreach (var destino in Enum.GetValues<DestinoEncaminhamento>())
            await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, destino);
        return c;
    }

    [Fact]
    public async Task Equipe_lista_todos_com_motivo()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioComTodosOsDestinosAsync();
        var sql = new List<string>();
        await using var db = fx.CriarContexto(sql.Add);
        var consultas = new EncaminhamentoQueries(db);
        var usuario = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorNap);
        var escopo = EscopoEncaminhamentos.Para(usuario);

        var lista = await consultas.ListarAsync(Filtro(c.Aluno), escopo, Hoje, Dias, ct);
        var detalhe = await consultas.ObterAsync(lista.Itens[0].Id, escopo, usuario, Hoje, Dias, ct);
        foreach (var s in sql) TestContext.Current.TestOutputHelper?.WriteLine(s);

        Assert.Equal(4, lista.Total);
        Assert.Equal(Enum.GetValues<DestinoEncaminhamento>().Order(), lista.Itens.Select(i => i.Destino!.Value).Order());
        var nomeCurso = await db.Cursos.Where(x => x.Id == c.Curso).Select(x => x.Nome).SingleAsync(ct);
        Assert.All(lista.Itens, i => Assert.Equal(nomeCurso, i.Curso));
        Assert.Equal(("Motivo restrito", "Observações internas"), (detalhe!.MotivoRestrito, detalhe.Observacoes));
        Assert.True(detalhe.PodeCancelar);
        Assert.False(string.IsNullOrEmpty(detalhe.Versao));
    }

    [Fact]
    public async Task Sip_lista_so_destinos_sip_e_clinica_com_motivo_sem_observacoes()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioComTodosOsDestinosAsync();
        await using var db = fx.CriarContexto();
        var consultas = new EncaminhamentoQueries(db);
        var usuario = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.Sip);
        var escopo = EscopoEncaminhamentos.Para(usuario);

        var lista = await consultas.ListarAsync(Filtro(c.Aluno), escopo, Hoje, Dias, ct);
        var detalhe = await consultas.ObterAsync(lista.Itens[0].Id, escopo, usuario, Hoje, Dias, ct);

        Assert.Equal(2, lista.Total);
        Assert.Equal([DestinoEncaminhamento.Sip, DestinoEncaminhamento.ClinicaEscola], lista.Itens.Select(i => i.Destino!.Value).Order());
        Assert.Equal("Motivo restrito", detalhe!.MotivoRestrito);
        Assert.Null(detalhe.Observacoes);
        Assert.Equal("Resumo compartilhável", detalhe.ResumoCompartilhavel);
        Assert.True(detalhe.PodeRegistrarRetorno);
        Assert.False(detalhe.PodeCancelar);
    }

    [Fact]
    public async Task Coordenador_lista_so_o_proprio_curso_sem_motivo_nem_observacoes()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioComTodosOsDestinosAsync();
        var outro = await CenarioAsync();
        await SalvarAsync(fx, outro.Aluno, outro.Curso, outro.Prof, DestinoEncaminhamento.Sip);
        await using var db = fx.CriarContexto();
        var consultas = new EncaminhamentoQueries(db);
        var usuario = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorCurso, c.Curso);
        var escopo = EscopoEncaminhamentos.Para(usuario);

        var lista = await consultas.ListarAsync(Filtro(), escopo, Hoje, Dias, ct);
        var detalhes = new List<EncaminhamentoDetalheDto>();
        foreach (var i in lista.Itens) detalhes.Add((await consultas.ObterAsync(i.Id, escopo, usuario, Hoje, Dias, ct))!);

        Assert.Equal(4, lista.Total);
        Assert.All(lista.Itens, i => Assert.Equal(c.Aluno, i.AlunoId));
        Assert.All(detalhes, d => Assert.Null(d.MotivoRestrito ?? d.Observacoes));
        // Resumo só no encaminhamento para a própria coordenação, como na linha do tempo; nos demais, nulo.
        Assert.Equal([(DestinoEncaminhamento.CoordenacaoCurso, "Resumo compartilhável")],
            detalhes.Where(d => d.ResumoCompartilhavel != null).Select(d => (d.Destino!.Value, d.ResumoCompartilhavel!)));
        Assert.Equal(3, detalhes.Count(d => d.ResumoCompartilhavel == null));
        Assert.All(detalhes, d => Assert.False(d.PodeCancelar));
        // Só o destino Coordenação de curso é atualizado pelo coordenador.
        Assert.Equal([DestinoEncaminhamento.CoordenacaoCurso], detalhes.Where(d => d.PodeRegistrarRetorno).Select(d => d.Destino!.Value));
    }

    [Fact]
    public async Task Coordenador_nao_ve_texto_de_evolucao_de_destino_sip()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var sip = await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, DestinoEncaminhamento.Sip,
            alterar: e => e.RegistrarRetorno(AcaoRetorno.Evolucao, null, "Texto do SIP", c.Prof, Hoje, Agora.AddMinutes(1)));
        var coordenacao = await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, DestinoEncaminhamento.CoordenacaoCurso,
            alterar: e => e.RegistrarRetorno(AcaoRetorno.Evolucao, null, "Texto da coordenação", c.Prof, Hoje, Agora.AddMinutes(1)));
        await using var db = fx.CriarContexto();
        var consultas = new EncaminhamentoQueries(db);
        var coordenador = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorCurso, c.Curso);
        var escopoCoordenador = EscopoEncaminhamentos.Para(coordenador);
        var equipe = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.ProfissionalNap);

        var doSip = await consultas.ObterAsync(sip.Id, escopoCoordenador, coordenador, Hoje, Dias, ct);
        var daCoordenacao = await consultas.ObterAsync(coordenacao.Id, escopoCoordenador, coordenador, Hoje, Dias, ct);
        var paraEquipe = await consultas.ObterAsync(sip.Id, EscopoEncaminhamentos.Para(equipe), equipe, Hoje, Dias, ct);

        var evolucao = Assert.Single(doSip!.Evolucoes);
        Assert.Null(evolucao.Texto);
        Assert.Equal((AcaoRetorno.Evolucao, EstadoEncaminhamento.Encaminhado), (evolucao.Acao, evolucao.EstadoNovo));
        Assert.Equal("Texto da coordenação", Assert.Single(daCoordenacao!.Evolucoes).Texto);
        Assert.Equal("Texto do SIP", Assert.Single(paraEquipe!.Evolucoes).Texto);
        Assert.StartsWith("Profissional", evolucao.Autor);
    }

    [Fact]
    public async Task Encaminhamento_antigo_fica_com_o_curso_da_criacao_apos_transferencia()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var cursoNovo = await fx.CriarCursoAsync("Curso novo do encaminhamento");
        await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, DestinoEncaminhamento.Sip);
        await using (var escrita = fx.CriarContexto())
        {
            var aluno = (await new AlunoRepository(escrita).ObterAsync(c.Aluno, ct))!;
            aluno.TransferirCurso(cursoNovo, Hoje, "Transferência", c.Prof, Agora.AddHours(1));
            await escrita.SaveChangesAsync(ct);
        }

        await using var db = fx.CriarContexto();
        var consultas = new EncaminhamentoQueries(db);
        var novo = await consultas.ListarAsync(Filtro(c.Aluno), Escopo(Perfis.CoordenadorCurso, cursoNovo), Hoje, Dias, ct);
        var antigo = await consultas.ListarAsync(Filtro(c.Aluno), Escopo(Perfis.CoordenadorCurso, c.Curso), Hoje, Dias, ct);

        Assert.Equal(0, novo.Total);
        Assert.Equal(1, antigo.Total);
        Assert.Equal(await db.Cursos.Where(x => x.Id == c.Curso).Select(x => x.Nome).SingleAsync(ct), antigo.Itens[0].Curso);
    }

    [Fact]
    public async Task Obter_fora_do_escopo_retorna_null()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var externo = await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, DestinoEncaminhamento.Externo);
        await using var db = fx.CriarContexto();
        var consultas = new EncaminhamentoQueries(db);
        var sip = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.Sip);
        var outroCoordenador = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorCurso, Guid.NewGuid());

        Assert.Null(await consultas.ObterAsync(externo.Id, EscopoEncaminhamentos.Para(sip), sip, Hoje, Dias, ct));
        Assert.Null(await consultas.ObterAsync(externo.Id, EscopoEncaminhamentos.Para(outroCoordenador), outroCoordenador, Hoje, Dias, ct));
        Assert.Throws<ForbiddenException>(() => Escopo(Perfis.Gestao));
        Assert.Throws<ForbiddenException>(() => Escopo(Perfis.CoordenadorCurso));
    }

    [Fact]
    public async Task Total_conta_so_o_escopo_autorizado()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        for (var i = 1; i <= 3; i++) await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, DestinoEncaminhamento.Sip, Hoje.AddDays(-i));
        for (var i = 1; i <= 2; i++) await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, DestinoEncaminhamento.Externo, Hoje.AddDays(-i));
        var sql = new List<string>();
        await using var db = fx.CriarContexto(sql.Add);
        var consultas = new EncaminhamentoQueries(db);
        var sip = Escopo(Perfis.Sip);

        var p1 = await consultas.ListarAsync(Filtro(c.Aluno, tamanho: 2), sip, Hoje, Dias, ct);
        var p2 = await consultas.ListarAsync(Filtro(c.Aluno, pagina: 2, tamanho: 2), sip, Hoje, Dias, ct);
        foreach (var s in sql) TestContext.Current.TestOutputHelper?.WriteLine(s);

        Assert.Equal((3, 3), (p1.Total, p2.Total));
        Assert.Equal([Hoje.AddDays(-1), Hoje.AddDays(-2), Hoje.AddDays(-3)], p1.Itens.Concat(p2.Itens).Select(i => i.Data));
        Assert.All(p1.Itens.Concat(p2.Itens), i => Assert.Equal(DestinoEncaminhamento.Sip, i.Destino));
    }

    [Fact]
    public async Task Filtro_sem_acolhimento_no_prazo_usa_dias_configurados()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var atrasado = await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, DestinoEncaminhamento.Sip, Hoje.AddDays(-8));
        var noPrazo = await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, DestinoEncaminhamento.Sip, Hoje.AddDays(-6));
        var acolhido = await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, DestinoEncaminhamento.Sip, Hoje.AddDays(-8),
            e => e.RegistrarRetorno(AcaoRetorno.Acolhimento, Hoje.AddDays(-1), null, c.Prof, Hoje, Agora));
        await using var db = fx.CriarContexto();
        var consultas = new EncaminhamentoQueries(db);
        var equipe = Escopo(Perfis.Administrador);

        var todos = await consultas.ListarAsync(Filtro(c.Aluno), equipe, Hoje, Dias, ct);
        var sim = await consultas.ListarAsync(Filtro(c.Aluno, true), equipe, Hoje, Dias, ct);
        var nao = await consultas.ListarAsync(Filtro(c.Aluno, false), equipe, Hoje, Dias, ct);
        var prazoMaior = await consultas.ListarAsync(Filtro(c.Aluno, true), equipe, Hoje, 10, ct);

        var sinal = todos.Itens.ToDictionary(i => i.Id, i => i.SemAcolhimentoNoPrazo);
        Assert.Equal((true, false, false), (sinal[atrasado.Id], sinal[noPrazo.Id], sinal[acolhido.Id]));
        Assert.Equal([atrasado.Id], sim.Itens.Select(i => i.Id));
        Assert.Equal(new[] { noPrazo.Id, acolhido.Id }.Order(), nao.Itens.Select(i => i.Id).Order());
        Assert.Equal(0, prazoMaior.Total);
    }

    [Fact]
    public async Task Cancelado_nao_permite_retorno_nem_cancelamento()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var cancelado = await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, DestinoEncaminhamento.Sip,
            alterar: e => e.Cancelar("Justificativa", c.Prof, Hoje, Agora.AddMinutes(1)));
        await using var db = fx.CriarContexto();
        var coordenacao = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorNap);

        var detalhe = await new EncaminhamentoQueries(db)
            .ObterAsync(cancelado.Id, EscopoEncaminhamentos.Para(coordenacao), coordenacao, Hoje, Dias, ct);

        Assert.Equal(EstadoEncaminhamento.Cancelado, detalhe!.Estado);
        Assert.Equal((false, false), (detalhe.PodeRegistrarRetorno, detalhe.PodeCancelar));
    }

    [Fact]
    public async Task Detalhe_do_coordenador_nao_le_colunas_restritas()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var e = await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, DestinoEncaminhamento.Sip,
            alterar: x => x.RegistrarRetorno(AcaoRetorno.Evolucao, null, "Texto do SIP", c.Prof, Hoje, Agora.AddMinutes(1)));
        var sql = new List<string>();
        await using var db = fx.CriarContexto(sql.Add);
        var coordenador = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorCurso, c.Curso);

        var detalhe = await new EncaminhamentoQueries(db)
            .ObterAsync(e.Id, EscopoEncaminhamentos.Para(coordenador), coordenador, Hoje, Dias, ct);
        foreach (var s in sql) TestContext.Current.TestOutputHelper?.WriteLine(s);

        Assert.NotNull(detalhe);
        var consulta = Assert.Single(sql);
        Assert.DoesNotContain("[MotivoRestrito]", consulta);
        Assert.DoesNotContain("[Observacoes]", consulta);
        // Texto da evolução só sai do banco dentro do CASE do destino Coordenação de curso.
        Assert.Single(consulta.Split("[Texto]").Skip(1));
        Assert.Contains("N'CoordenacaoCurso' THEN [e0].[Texto]", consulta);
        Assert.Null(Assert.Single(detalhe.Evolucoes).Texto);
    }

    [Fact]
    public async Task Coordenador_nao_recebe_especialidade_nem_destino_de_encaminhamento_clinico()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var sip = await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, DestinoEncaminhamento.Sip);
        var coordenacao = await SalvarAsync(fx, c.Aluno, c.Curso, c.Prof, DestinoEncaminhamento.CoordenacaoCurso);
        var sql = new List<string>();
        await using var db = fx.CriarContexto(sql.Add);
        var consultas = new EncaminhamentoQueries(db);
        var coordenador = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorCurso, c.Curso);
        var escopo = EscopoEncaminhamentos.Para(coordenador);

        var lista = await consultas.ListarAsync(Filtro(c.Aluno), escopo, Hoje, Dias, ct);
        var doSip = await consultas.ObterAsync(sip.Id, escopo, coordenador, Hoje, Dias, ct);
        var daCoordenacao = await consultas.ObterAsync(coordenacao.Id, escopo, coordenador, Hoje, Dias, ct);
        foreach (var s in sql) TestContext.Current.TestOutputHelper?.WriteLine(s);

        var itens = lista.Itens.ToDictionary(i => i.Id);
        Assert.Equal((null, null), (itens[sip.Id].Especialidade, itens[sip.Id].Destino));
        Assert.Equal((Especialidade.CoordenacaoCurso, DestinoEncaminhamento.CoordenacaoCurso),
            (itens[coordenacao.Id].Especialidade, itens[coordenacao.Id].Destino));
        Assert.Equal((null, null), (doSip!.Especialidade, doSip.Destino));
        Assert.Equal((Especialidade.CoordenacaoCurso, DestinoEncaminhamento.CoordenacaoCurso),
            (daCoordenacao!.Especialidade, daCoordenacao.Destino));
        // O ocultamento é feito no SQL (CASE), não depois de materializar.
        Assert.All(sql.Where(s => s.Contains("[Especialidade]")), s => Assert.Contains("CASE", s));
    }

    [Fact]
    public async Task Filtro_de_especialidade_ou_destino_do_coordenador_so_casa_destino_coordenacao()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioComTodosOsDestinosAsync();
        await using var db = fx.CriarContexto();
        var consultas = new EncaminhamentoQueries(db);
        var coordenador = Escopo(Perfis.CoordenadorCurso, c.Curso);
        FiltroEncaminhamentos Com(Especialidade? especialidade, DestinoEncaminhamento? destino) =>
            new(c.Aluno, null, especialidade, destino, null, null, null, null, 1, 20);

        var psicologia = await consultas.ListarAsync(Com(Especialidade.Psicologia, null), coordenador, Hoje, Dias, ct);
        var sip = await consultas.ListarAsync(Com(null, DestinoEncaminhamento.Sip), coordenador, Hoje, Dias, ct);
        var doCurso = await consultas.ListarAsync(Com(null, DestinoEncaminhamento.CoordenacaoCurso), coordenador, Hoje, Dias, ct);
        var equipe = await consultas.ListarAsync(Com(Especialidade.Psicologia, null), Escopo(Perfis.Administrador), Hoje, Dias, ct);

        Assert.Equal((0, 0, 1, 3), (psicologia.Total, sip.Total, doCurso.Total, equipe.Total));
    }
}
