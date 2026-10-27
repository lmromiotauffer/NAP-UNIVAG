using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Peis;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Peis;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Tests;

public class PeiQueriesTests(SqlServerFixture fx)
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(-4));

    private static ConteudoPei Conteudo() => new("Demanda interna sigilosa", "Necessidades", null, "Orientações", null, "Cuidados",
        PeriodoVigencia.Criar(new DateOnly(2026, 8, 1), new DateOnly(2027, 7, 31)));

    /// <summary>Grava um PEI com a versão 1 levada até <paramref name="ate"/>; a versão 2, se pedida, nasce em rascunho.</summary>
    internal static async Task<Pei> SalvarPeiAsync(SqlServerFixture fx, Guid alunoId, Guid prof, Guid curso, EstadoVersaoPei ate,
        DateTimeOffset? enviadaEm = null, bool comVersao2 = false)
    {
        var coord = await fx.CriarProfissionalAsync(Perfis.CoordenadorNap);
        var pei = Pei.Criar(alunoId, prof, Conteudo(), prof, Agora);
        var v1 = pei.Versoes.Single();
        if (ate >= EstadoVersaoPei.EmRevisao) v1.EnviarParaRevisao(enviadaEm ?? Agora.AddMinutes(1));
        var hash = CalculadoraHashPei.Calcular(v1.Conteudo);
        if (ate >= EstadoVersaoPei.AguardandoAssinaturas) v1.AprovarRevisao(coord, Agora.AddMinutes(2));
        if (ate >= EstadoVersaoPei.Assinada)
        {
            v1.RegistrarAssinatura(PapelAssinatura.Profissional, prof, "Profissional", hash, "e1", Agora.AddMinutes(3));
            v1.RegistrarAssinatura(PapelAssinatura.Aluno, null, "Aluno", hash, "e2", Agora.AddMinutes(4));
        }
        if (ate >= EstadoVersaoPei.Publicada) pei.Publicar(v1, coord, curso, Agora.AddMinutes(5));
        if (comVersao2) pei.NovaVersao(prof, Agora.AddMinutes(6));
        await using var db = fx.CriarContexto();
        db.Add(pei);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return pei;
    }

    private async Task<(Guid Prof, Guid Curso, Guid Aluno)> CenarioAsync()
    {
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        return (prof, curso, aluno.Id);
    }

    private static UsuarioAtualTeste Equipe(string perfil = Perfis.CoordenadorNap, Guid? id = null) => new(id ?? Guid.NewGuid(), perfil);

    private static UsuarioAtualTeste Coordenador(Guid curso) => new(Guid.NewGuid(), Perfis.CoordenadorCurso, curso);

    [Fact]
    public async Task Equipe_ve_todas_as_versoes()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var pei = await SalvarPeiAsync(fx, c.Aluno, c.Prof, c.Curso, EstadoVersaoPei.Publicada, comVersao2: true);
        var v2 = pei.Versoes.Single(v => v.Numero == 2);
        await using var db = fx.CriarContexto();
        var consultas = new PeiQueries(db);
        var u = Equipe();

        var lista = await consultas.ListarDoAlunoAsync(c.Aluno, EscopoPeis.Para(u), u.Id, ct);
        var detalhe = await consultas.ObterVersaoAsync(v2.Id, EscopoPeis.Para(u), u, ct);

        var item = Assert.Single(lista!);
        Assert.Equal([2, 1], item.Versoes.Select(v => v.Numero));
        Assert.Equal([EstadoVersaoPei.Rascunho, EstadoVersaoPei.Publicada], item.Versoes.Select(v => v.Estado));
        Assert.Equal(new DateOnly(2027, 7, 31), item.Versoes[0].VigenciaFim);
        Assert.NotNull(detalhe);
        Assert.Equal("Demanda interna sigilosa", detalhe.Conteudo.DemandaInterna);
        Assert.Equal(("Aluno de teste", await db.Cursos.Where(x => x.Id == c.Curso).Select(x => x.Nome).SingleAsync(ct), 2), (detalhe.Aluno, detalhe.Curso, detalhe.Numero));
        Assert.Null(detalhe.HashConteudo);
        Assert.NotEmpty(detalhe.Versao);
        Assert.Equal(c.Aluno, detalhe.AlunoId);
    }

    [Fact]
    public async Task Coordenador_so_ve_publicadas_do_proprio_curso()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var pei = await SalvarPeiAsync(fx, c.Aluno, c.Prof, c.Curso, EstadoVersaoPei.Publicada, comVersao2: true);
        var v1 = pei.Versoes.Single(v => v.Numero == 1);
        var outroCurso = await fx.CriarCursoAsync();
        await using var db = fx.CriarContexto();
        var consultas = new PeiQueries(db);
        var meu = Coordenador(c.Curso);
        var alheio = Coordenador(outroCurso);

        var lista = await consultas.ListarDoAlunoAsync(c.Aluno, EscopoPeis.Para(meu), meu.Id, ct);
        var detalhe = await consultas.ObterVersaoAsync(v1.Id, EscopoPeis.Para(meu), meu, ct);

        Assert.Equal([1], Assert.Single(lista!).Versoes.Select(v => v.Numero));
        Assert.NotNull(detalhe);
        Assert.Equal(2, detalhe.Assinaturas.Count);
        Assert.All(detalhe.Assinaturas, a => Assert.Equal("Demonstrativa", a.Metodo));
        Assert.Equal(CalculadoraHashPei.Calcular(Conteudo()), detalhe.HashConteudo);
        // Coordenador de outro curso: aluno fora do escopo (404) e versão inexistente para ele.
        Assert.Null(await consultas.ListarDoAlunoAsync(c.Aluno, EscopoPeis.Para(alheio), alheio.Id, ct));
        Assert.Null(await consultas.ObterVersaoAsync(v1.Id, EscopoPeis.Para(alheio), alheio, ct));
        Assert.Null(await consultas.ListarDoAlunoAsync(Guid.NewGuid(), EscopoPeis.Para(meu), meu.Id, ct));
    }

    [Fact]
    public async Task Coordenador_nao_le_demanda_interna_no_sql()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var pei = await SalvarPeiAsync(fx, c.Aluno, c.Prof, c.Curso, EstadoVersaoPei.Publicada);
        var v1 = pei.Versoes.Single();
        var sql = new List<string>();
        await using var db = fx.CriarContexto(sql.Add);
        var consultas = new PeiQueries(db);
        var coordenador = Coordenador(c.Curso);

        var detalhe = await consultas.ObterVersaoAsync(v1.Id, EscopoPeis.Para(coordenador), coordenador, ct);
        await consultas.ListarDoAlunoAsync(c.Aluno, EscopoPeis.Para(coordenador), coordenador.Id, ct);
        foreach (var s in sql) TestContext.Current.TestOutputHelper?.WriteLine(s);

        Assert.NotNull(detalhe);
        Assert.Null(detalhe.Conteudo.DemandaInterna);
        Assert.Equal("Necessidades", detalhe.Conteudo.NecessidadesEducacionais);
        Assert.NotEmpty(sql);
        Assert.All(sql, s => Assert.DoesNotContain("DemandaInterna", s));
        // Controle: para a Equipe a coluna é lida.
        var equipe = Equipe();
        sql.Clear();
        await consultas.ObterVersaoAsync(v1.Id, EscopoPeis.Para(equipe), equipe, ct);
        Assert.Contains(sql, s => s.Contains("Conteudo_DemandaInterna"));
    }

    [Fact]
    public async Task Versao_publicada_antes_da_transferencia_fica_no_curso_da_publicacao()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var pei = await SalvarPeiAsync(fx, c.Aluno, c.Prof, c.Curso, EstadoVersaoPei.Publicada);
        var v1 = pei.Versoes.Single();
        var cursoNovo = await fx.CriarCursoAsync();
        await using (var escrita = fx.CriarContexto())
        {
            // Transferência real: o recorte do coordenador segue o histórico de vínculos (curso da época).
            var aluno = (await new Persistencia.Repositorios.AlunoRepository(escrita).ObterAsync(c.Aluno, ct))!;
            aluno.TransferirCurso(cursoNovo, new DateOnly(2026, 10, 4), "Mudança de curso", c.Prof,
                new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
            await escrita.SaveChangesAsync(ct);
        }
        await using var db = fx.CriarContexto();
        var consultas = new PeiQueries(db);
        var antigo = Coordenador(c.Curso);
        var novo = Coordenador(cursoNovo);

        var detalhe = await consultas.ObterVersaoAsync(v1.Id, EscopoPeis.Para(antigo), antigo, ct);
        Assert.NotNull(detalhe);
        // Detalhe (e o PDF, que sai dele) mostram o curso da publicação, não o atual.
        Assert.Equal(await db.Cursos.Where(x => x.Id == c.Curso).Select(x => x.Nome).SingleAsync(ct), detalhe.Curso);
        Assert.Single((await consultas.ListarDoAlunoAsync(c.Aluno, EscopoPeis.Para(antigo), antigo.Id, ct))!);
        // O coordenador do curso atual vê a versão publicada em vigor (é quem aplica o plano) e pode registrar ciência.
        var doNovo = await consultas.ObterVersaoAsync(v1.Id, EscopoPeis.Para(novo), novo, ct);
        Assert.True(doNovo!.Acoes.PodeRegistrarCiencia);
        Assert.Single((await consultas.ListarDoAlunoAsync(c.Aluno, EscopoPeis.Para(novo), novo.Id, ct))!);
    }

    [Fact]
    public async Task Rascunho_por_id_para_coordenador_retorna_null()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var rascunho = (await SalvarPeiAsync(fx, c.Aluno, c.Prof, c.Curso, EstadoVersaoPei.Rascunho)).Versoes.Single();
        var c2 = await CenarioAsync();
        var emRevisao = (await SalvarPeiAsync(fx, c2.Aluno, c2.Prof, c2.Curso, EstadoVersaoPei.EmRevisao)).Versoes.Single();
        await using var db = fx.CriarContexto();
        var consultas = new PeiQueries(db);
        var coordenador = Coordenador(c.Curso);
        var coordenador2 = Coordenador(c2.Curso);

        Assert.Null(await consultas.ObterVersaoAsync(rascunho.Id, EscopoPeis.Para(coordenador), coordenador, ct));
        Assert.Null(await consultas.ObterVersaoAsync(emRevisao.Id, EscopoPeis.Para(coordenador2), coordenador2, ct));
        Assert.Empty((await consultas.ListarDoAlunoAsync(c.Aluno, EscopoPeis.Para(coordenador), coordenador.Id, ct))!);
    }

    [Fact]
    public async Task Acoes_respeitam_estado_e_perfil()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var outroProf = await fx.CriarProfissionalAsync();
        var rascunho = (await SalvarPeiAsync(fx, c.Aluno, c.Prof, c.Curso, EstadoVersaoPei.Rascunho)).Versoes.Single();
        var c2 = await CenarioAsync();
        var revisao = (await SalvarPeiAsync(fx, c2.Aluno, c2.Prof, c2.Curso, EstadoVersaoPei.EmRevisao)).Versoes.Single();
        var c3 = await CenarioAsync();
        var aguardando = (await SalvarPeiAsync(fx, c3.Aluno, c3.Prof, c3.Curso, EstadoVersaoPei.AguardandoAssinaturas)).Versoes.Single();
        var c4 = await CenarioAsync();
        var assinada = (await SalvarPeiAsync(fx, c4.Aluno, c4.Prof, c4.Curso, EstadoVersaoPei.Assinada)).Versoes.Single();
        var c5 = await CenarioAsync();
        var publicadaPei = await SalvarPeiAsync(fx, c5.Aluno, c5.Prof, c5.Curso, EstadoVersaoPei.Publicada);
        var publicada = publicadaPei.Versoes.Single();
        await using var db = fx.CriarContexto();
        var consultas = new PeiQueries(db);

        async Task<AcoesVersaoDto> Acoes(Guid versaoId, UsuarioAtualTeste u) =>
            (await consultas.ObterVersaoAsync(versaoId, EscopoPeis.Para(u), u, ct))!.Acoes;
        var responsavel = Equipe(Perfis.ProfissionalNap, c.Prof);

        // Rascunho: a Equipe edita e envia; a coordenação também (é Equipe NAP), mas não aprova nem publica.
        var a = await Acoes(rascunho.Id, responsavel);
        Assert.True(a.PodeEditar && a.PodeEnviarRevisao);
        Assert.False(a.PodeAprovar || a.PodeDevolver || a.PodeAssinarComoProfissional || a.PodeGerarAcessoAluno || a.PodePublicar
            || a.PodeArquivar || a.PodeRegistrarCiencia);
        Assert.False(a.PodeCriarNovaVersao);

        // Em revisão: só a coordenação aprova/devolve; ninguém edita.
        a = await Acoes(revisao.Id, Equipe(Perfis.CoordenadorNap));
        Assert.True(a.PodeAprovar && a.PodeDevolver);
        Assert.False(a.PodeEditar || a.PodeEnviarRevisao || a.PodeCriarNovaVersao);
        a = await Acoes(revisao.Id, Equipe(Perfis.ProfissionalNap, c2.Prof));
        Assert.False(a.PodeAprovar || a.PodeDevolver || a.PodeEditar);

        // Aguardando assinaturas: só o responsável assina; Equipe gera o acesso do aluno; coordenação devolve.
        Assert.True((await Acoes(aguardando.Id, Equipe(Perfis.ProfissionalNap, c3.Prof))).PodeAssinarComoProfissional);
        a = await Acoes(aguardando.Id, Equipe(Perfis.ProfissionalNap, outroProf));
        Assert.False(a.PodeAssinarComoProfissional);
        Assert.True(a.PodeGerarAcessoAluno);
        Assert.True((await Acoes(aguardando.Id, Equipe(Perfis.Administrador))).PodeDevolver);

        // Assinada: coordenação publica; ninguém assina de novo.
        a = await Acoes(assinada.Id, Equipe(Perfis.Administrador));
        Assert.True(a.PodePublicar);
        Assert.False(a.PodeAprovar || a.PodeAssinarComoProfissional || a.PodeGerarAcessoAluno);

        // Publicada: coordenação arquiva e cria nova versão; o coordenador do curso registra ciência (uma vez).
        a = await Acoes(publicada.Id, Equipe(Perfis.CoordenadorNap));
        Assert.True(a.PodeArquivar && a.PodeCriarNovaVersao);
        Assert.False(a.PodePublicar || a.PodeRegistrarCiencia);
        var coordenador = new UsuarioAtualTeste(await fx.CriarProfissionalAsync(Perfis.CoordenadorCurso), Perfis.CoordenadorCurso, c5.Curso);
        a = await Acoes(publicada.Id, coordenador);
        Assert.True(a.PodeRegistrarCiencia);
        Assert.False(a.PodeArquivar || a.PodeCriarNovaVersao || a.PodeEditar || a.PodePublicar);

        await using (var escrita = fx.CriarContexto())
        {
            var pei = await escrita.Peis.Include(p => p.Versoes).ThenInclude(v => v.Ciencias).SingleAsync(p => p.Id == publicadaPei.Id, ct);
            pei.Versoes.Single().RegistrarCiencia(coordenador.Id, Agora.AddDays(1));
            await escrita.SaveChangesAsync(ct);
        }
        await using var depois = fx.CriarContexto();
        var aposCiencia = (await new PeiQueries(depois).ObterVersaoAsync(publicada.Id, EscopoPeis.Para(coordenador), coordenador, ct))!;
        Assert.False(aposCiencia.Acoes.PodeRegistrarCiencia);
        Assert.Single(aposCiencia.Ciencias);
        var resumo = (await new PeiQueries(depois).ListarDoAlunoAsync(c5.Aluno, EscopoPeis.Para(coordenador), coordenador.Id, ct))!;
        Assert.True(Assert.Single(Assert.Single(resumo).Versoes).CienciaRegistradaPorMim);
    }
}
