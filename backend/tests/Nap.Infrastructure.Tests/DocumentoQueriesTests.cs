using Nap.Application.Features.Documentos;
using Nap.Domain.Comum;
using Nap.Domain.Documentos;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Consultas;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class DocumentoQueriesTests(SqlServerFixture fx)
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(-3));

    private async Task<(Guid Prof, Guid Curso, Guid Aluno)> CenarioAsync()
    {
        var prof = await fx.CriarProfissionalAsync(nome: "Ana Profissional");
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        return (prof, curso, aluno.Id);
    }

    private async Task<Documento> SalvarAsync((Guid Prof, Guid Curso, Guid Aluno) c, int minutos = 0, bool compartilhar = false,
        bool arquivar = false)
    {
        var d = Documento.Criar(c.Aluno, c.Curso, CategoriaDocumento.Laudo, "Laudo neurológico", "laudo.pdf", "application/pdf",
            10, new string('a', 64), Guid.NewGuid().ToString("N"), c.Prof, Agora.AddMinutes(minutos));
        if (compartilhar) d.Compartilhar("Necessário para adaptação", c.Prof, d.CursoId, Agora.AddMinutes(minutos));
        if (arquivar) d.Arquivar("Documento substituído", c.Prof, Agora.AddMinutes(minutos));
        await using var db = fx.CriarContexto();
        db.Add(d);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return d;
    }

    private static EscopoDocumentos Escopo(string perfil, Guid? cursoId = null, bool incluirArquivados = false) =>
        EscopoDocumentos.Para(new UsuarioAtualTeste(Guid.NewGuid(), perfil, cursoId), incluirArquivados);

    [Fact]
    public async Task Coordenador_lista_so_compartilhados_do_proprio_curso_nao_arquivados()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        await SalvarAsync(c, 0);
        var compartilhado = await SalvarAsync(c, 1, compartilhar: true);
        await SalvarAsync(c, 2, compartilhar: true, arquivar: true);
        await using var db = fx.CriarContexto();
        var consultas = new DocumentoQueries(db);

        var lista = await consultas.ListarAsync(c.Aluno, Escopo(Perfis.CoordenadorCurso, c.Curso, incluirArquivados: true), 1, 20, ct);
        var outroCurso = await consultas.ListarAsync(c.Aluno, Escopo(Perfis.CoordenadorCurso, Guid.NewGuid()), 1, 20, ct);

        var item = Assert.Single(lista!.Itens);
        Assert.Equal(compartilhado.Id, item.Id);
        Assert.Equal(("Ana Profissional", "Ana Profissional"), (item.Autor, item.CompartilhadoPor));
        Assert.Equal(VisibilidadeDocumento.CompartilhadoCoordenacao, item.Visibilidade);
        Assert.False(string.IsNullOrEmpty(item.Versao));
        Assert.Null(outroCurso);
    }

    [Fact]
    public async Task Equipe_lista_em_ordem_decrescente_e_sem_compartilhador_apos_revogar()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var antigo = await SalvarAsync(c, 0);
        var novo = await SalvarAsync(c, 5, compartilhar: true);
        await using (var db = fx.CriarContexto())
        {
            var d = (await new DocumentoRepository(db).ObterAsync(novo.Id, ct))!;
            d.RevogarCompartilhamento("Não é mais necessário", c.Prof, Agora.AddMinutes(6));
            await new UnidadeDeTrabalho(db).SalvarAsync(ct);
        }
        await using var leitura = fx.CriarContexto();

        var lista = await new DocumentoQueries(leitura).ListarAsync(c.Aluno, Escopo(Perfis.ProfissionalNap), 1, 20, ct);

        Assert.Equal([novo.Id, antigo.Id], lista!.Itens.Select(i => i.Id));
        Assert.Equal(VisibilidadeDocumento.RestritoNap, lista.Itens[0].Visibilidade);
        Assert.Null(lista.Itens[0].CompartilhadoPor);
        Assert.Null(lista.Itens[0].CompartilhadoEm);
    }

    [Fact]
    public async Task Documento_fica_com_o_curso_da_criacao_apos_transferencia()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var cursoNovo = await fx.CriarCursoAsync();
        var d = await SalvarAsync(c, compartilhar: true);
        await using (var db = fx.CriarContexto())
        {
            var aluno = (await new AlunoRepository(db).ObterAsync(c.Aluno, ct))!;
            aluno.TransferirCurso(cursoNovo, new DateOnly(2026, 10, 20), "Transferência", c.Prof, Agora.AddMinutes(1));
            await new UnidadeDeTrabalho(db).SalvarAsync(ct);
        }
        await using var leitura = fx.CriarContexto();
        var consultas = new DocumentoQueries(leitura);
        var antigo = Escopo(Perfis.CoordenadorCurso, c.Curso);
        var novo = Escopo(Perfis.CoordenadorCurso, cursoNovo);

        // O coordenador novo vê o aluno, mas não o documento gravado com o curso anterior.
        Assert.Empty((await consultas.ListarAsync(c.Aluno, novo, 1, 20, ct))!.Itens);
        Assert.Null(await consultas.ObterAsync(d.Id, novo, ct));
        // O antigo mantém a ficha (curso da época) e o documento compartilhado enquanto coordenava o aluno.
        Assert.Equal([d.Id], (await consultas.ListarAsync(c.Aluno, antigo, 1, 20, ct))!.Itens.Select(i => i.Id));
        Assert.NotNull(await consultas.ObterArquivoAsync(d.Id, antigo, ct));
    }

    [Fact]
    public async Task Compartilhar_apos_transferencia_vai_para_o_curso_atual()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var cursoNovo = await fx.CriarCursoAsync();
        var d = await SalvarAsync(c);
        await using (var db = fx.CriarContexto())
        {
            var aluno = (await new AlunoRepository(db).ObterAsync(c.Aluno, ct))!;
            aluno.TransferirCurso(cursoNovo, new DateOnly(2026, 10, 20), "Transferência", c.Prof, Agora.AddMinutes(1));
            var documento = (await new DocumentoRepository(db).ObterAsync(d.Id, ct))!;
            documento.Compartilhar("Necessário para adaptação", c.Prof, aluno.CursoAtualId, Agora.AddMinutes(2));
            await new UnidadeDeTrabalho(db).SalvarAsync(ct);
        }
        await using var leitura = fx.CriarContexto();
        var consultas = new DocumentoQueries(leitura);

        Assert.NotNull(await consultas.ObterArquivoAsync(d.Id, Escopo(Perfis.CoordenadorCurso, cursoNovo), ct));
        Assert.Null(await consultas.ObterArquivoAsync(d.Id, Escopo(Perfis.CoordenadorCurso, c.Curso), ct));
    }

    [Fact]
    public async Task Arquivados_so_para_coordenacao_com_flag()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        await SalvarAsync(c, 0);
        var arquivado = await SalvarAsync(c, 1, arquivar: true);
        await using var db = fx.CriarContexto();
        var consultas = new DocumentoQueries(db);

        async Task<int> Total(EscopoDocumentos e) => (await consultas.ListarAsync(c.Aluno, e, 1, 20, ct))!.Total;

        Assert.Equal(2, await Total(Escopo(Perfis.CoordenadorNap, incluirArquivados: true)));
        Assert.Equal(2, await Total(Escopo(Perfis.Administrador, incluirArquivados: true)));
        Assert.Equal(1, await Total(Escopo(Perfis.CoordenadorNap)));
        Assert.Equal(1, await Total(Escopo(Perfis.ProfissionalNap, incluirArquivados: true)));
        Assert.True((await consultas.ObterAsync(arquivado.Id, Escopo(Perfis.Administrador, incluirArquivados: true), ct))!.Arquivado);
        Assert.Null(await consultas.ObterArquivoAsync(arquivado.Id, Escopo(Perfis.ProfissionalNap, incluirArquivados: true), ct));
    }

    [Fact]
    public async Task Download_fora_do_escopo_retorna_null()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        var restrito = await SalvarAsync(c);
        var compartilhado = await SalvarAsync(c, compartilhar: true);
        await using var db = fx.CriarContexto();
        var consultas = new DocumentoQueries(db);
        var coordenador = Escopo(Perfis.CoordenadorCurso, c.Curso);

        Assert.Null(await consultas.ObterArquivoAsync(restrito.Id, coordenador, ct));
        Assert.Null(await consultas.ObterArquivoAsync(compartilhado.Id, Escopo(Perfis.CoordenadorCurso, Guid.NewGuid()), ct));
        Assert.Null(await consultas.ObterArquivoAsync(Guid.NewGuid(), Escopo(Perfis.ProfissionalNap), ct));
        var arquivo = await consultas.ObterArquivoAsync(compartilhado.Id, coordenador, ct);
        Assert.Equal((compartilhado.ChaveArmazenamento, "laudo.pdf", "application/pdf"),
            (arquivo!.Chave, arquivo.NomeArquivo, arquivo.TipoConteudo));
    }

    [Fact]
    public async Task Total_conta_so_o_recorte()
    {
        var ct = TestContext.Current.CancellationToken;
        var c = await CenarioAsync();
        for (var i = 0; i < 3; i++) await SalvarAsync(c, i, compartilhar: true);
        for (var i = 3; i < 5; i++) await SalvarAsync(c, i);
        await using var db = fx.CriarContexto();
        var consultas = new DocumentoQueries(db);

        var coordenador = await consultas.ListarAsync(c.Aluno, Escopo(Perfis.CoordenadorCurso, c.Curso), 1, 2, ct);
        var equipe = await consultas.ListarAsync(c.Aluno, Escopo(Perfis.ProfissionalNap), 2, 2, ct);

        Assert.Equal((3, 2), (coordenador!.Total, coordenador.Itens.Count));
        Assert.Equal((5, 2), (equipe!.Total, equipe.Itens.Count));
    }
}
