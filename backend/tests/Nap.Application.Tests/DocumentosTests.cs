using System.Security.Cryptography;
using System.Text;
using Nap.Application.Comum;
using Nap.Application.Features.Documentos.Commands;
using Nap.Application.Features.Documentos.Queries;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Alunos;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Documentos;

namespace Nap.Application.Tests;

public class DocumentosTests
{
    private static readonly byte[] Pdf = [.. "%PDF-1.7 conteúdo"u8];
    private readonly Guid _curso = Guid.NewGuid();
    private readonly AlunoRepositoryEmMemoria _alunos = new();
    private readonly DocumentoRepositoryEmMemoria _repo = new();
    private readonly List<string> _ordem = [];
    private readonly ArmazenamentoEmMemoria _armazenamento;
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly Aluno _aluno;

    public DocumentosTests()
    {
        _armazenamento = new(_ordem);
        _aluno = Aluno.Criar("Maria da Silva", Matricula.Criar("2024001"), _curso, 3, new DateOnly(2026, 8, 1),
            Guid.NewGuid(), null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        _alunos.Alunos.Add(_aluno);
    }

    private static UsuarioAtualFalso Profissional() => new(Perfis.ProfissionalNap);

    private AnexarDocumentoHandler AnexarHandler(IUsuarioAtual u, UnidadeDeTrabalhoFalsa? uow = null, long limite = 5_242_880) =>
        new(new AnexarDocumentoValidator(), _repo, _alunos, _armazenamento, new OpcoesArquivos { LimiteBytes = limite }, u,
            new RelogioFixo(), _auditoria, uow ?? new UnidadeDeTrabalhoFalsa());

    private AnexarDocumentoCommand Anexar(string nome, byte[] conteudo) =>
        new(_aluno.Id, CategoriaDocumento.Laudo, "Laudo neurológico", nome, new MemoryStream(conteudo));

    private Documento Semear(string? chave = null)
    {
        var d = Documento.Criar(_aluno.Id, _curso, CategoriaDocumento.Laudo, "Laudo neurológico", "laudo.pdf", "application/pdf",
            Pdf.Length, "00", chave ?? Guid.NewGuid().ToString("N"), Guid.NewGuid(), DateTimeOffset.UtcNow);
        _repo.Documentos.Add(d);
        return d;
    }

    [Fact]
    public async Task Anexar_grava_arquivo_e_documento_com_curso_atual_do_aluno()
    {
        var id = await AnexarHandler(Profissional()).Handle(Anexar("laudo.pdf", Pdf), TestContext.Current.CancellationToken);

        var d = Assert.Single(_repo.Documentos);
        Assert.Equal(id, d.Id);
        Assert.Equal(_curso, d.CursoId);
        Assert.Equal(("application/pdf", (long)Pdf.Length), (d.TipoConteudo, d.TamanhoBytes));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Pdf)), d.Sha256);
        Assert.Equal(Pdf, _armazenamento.Arquivos[d.ChaveArmazenamento]);
        Assert.Equal(VisibilidadeDocumento.RestritoNap, d.Visibilidade);
        Assert.Equal("documento.anexar", Assert.Single(_auditoria.Registros).Acao);
    }

    [Fact]
    public async Task Anexar_valida_conteudo_antes_de_gravar()
    {
        var erro = await Assert.ThrowsAsync<ValidationException>(() =>
            AnexarHandler(Profissional()).Handle(Anexar("laudo.pdf", Encoding.UTF8.GetBytes("não é PDF")),
                TestContext.Current.CancellationToken));

        Assert.True(erro.Erros.ContainsKey("arquivo"));
        Assert.Equal(0, _armazenamento.Gravacoes);
        Assert.Empty(_repo.Documentos);
    }

    [Fact]
    public async Task Anexar_le_no_maximo_um_byte_alem_do_limite()
    {
        var conteudo = new MemoryStream([.. Pdf, .. new byte[1000]]);

        var erro = await Assert.ThrowsAsync<ValidationException>(() =>
            AnexarHandler(Profissional(), limite: 10).Handle(
                new AnexarDocumentoCommand(_aluno.Id, CategoriaDocumento.Laudo, "Laudo neurológico", "laudo.pdf", conteudo),
                TestContext.Current.CancellationToken));

        Assert.Contains("excede o limite", Assert.Single(erro.Erros["arquivo"]));
        Assert.Equal(11, conteudo.Position);
        Assert.Equal(0, _armazenamento.Gravacoes);
    }

    [Fact]
    public async Task Anexar_com_aluno_inexistente_retorna_404()
    {
        var comando = Anexar("laudo.pdf", Pdf) with { AlunoId = Guid.NewGuid() };

        var erro = await Assert.ThrowsAsync<NotFoundException>(() =>
            AnexarHandler(Profissional()).Handle(comando, TestContext.Current.CancellationToken));

        Assert.Equal("Aluno não encontrado.", erro.Message);
        Assert.Equal(0, _armazenamento.Gravacoes);
    }

    [Fact]
    public async Task Anexar_rejeita_descricao_invalida_antes_de_gravar()
    {
        var comando = Anexar("laudo.pdf", Pdf) with { Descricao = " ab " };

        var erro = await Assert.ThrowsAsync<ValidationException>(() =>
            AnexarHandler(Profissional()).Handle(comando, TestContext.Current.CancellationToken));

        Assert.True(erro.Erros.ContainsKey("descricao"));
        Assert.Equal(0, _armazenamento.Gravacoes);
        Assert.Empty(_repo.Documentos);
    }

    [Fact]
    public async Task Arquivar_com_motivo_curto_retorna_400_no_campo()
    {
        var d = Semear();
        var handler = new ArquivarDocumentoHandler(new ArquivarDocumentoValidator(), _repo, Profissional(), new RelogioFixo(),
            _auditoria, new UnidadeDeTrabalhoFalsa());

        var erro = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new ArquivarDocumentoCommand(d.Id, "ab", "v1"), TestContext.Current.CancellationToken));

        Assert.True(erro.Erros.ContainsKey("motivo"));
        Assert.False(d.Arquivado);
    }

    [Fact]
    public async Task Falha_no_banco_remove_o_arquivo()
    {
        var uow = new UnidadeDeTrabalhoFalsa { FalharAoSalvar = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AnexarHandler(Profissional(), uow).Handle(Anexar("laudo.pdf", Pdf), TestContext.Current.CancellationToken));

        Assert.Equal(1, _armazenamento.Gravacoes);
        Assert.Empty(_armazenamento.Arquivos);
    }

    [Fact]
    public async Task Commit_com_resposta_perdida_mantem_o_arquivo()
    {
        var uow = new UnidadeDeTrabalhoFalsa { FalharAoSalvar = true };
        _repo.CommitApesarDaFalha = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => AnexarHandler(Profissional(), uow).Handle(Anexar("laudo.pdf", Pdf), TestContext.Current.CancellationToken));

        Assert.Single(_armazenamento.Arquivos);
    }

    [Theory]
    [InlineData(Perfis.Sip, null, typeof(ForbiddenException))]
    [InlineData(Perfis.CoordenadorCurso, null, typeof(ForbiddenException))]
    [InlineData(Perfis.CoordenadorCurso, "outro curso", typeof(NotFoundException))]
    public async Task Sip_e_coordenador_fora_do_curso_nao_anexam(string perfil, string? curso, Type erro)
    {
        var usuario = new UsuarioAtualFalso(perfil, curso is null ? null : Guid.NewGuid());

        await Assert.ThrowsAsync(erro, () =>
            AnexarHandler(usuario).Handle(Anexar("laudo.pdf", Pdf), TestContext.Current.CancellationToken));

        Assert.Equal(0, _armazenamento.Gravacoes);
        Assert.Empty(_repo.Documentos);
    }

    [Fact]
    public async Task Coordenador_do_curso_anexa_ja_compartilhado()
    {
        var coordenador = new UsuarioAtualFalso(Perfis.CoordenadorCurso, _curso);

        await AnexarHandler(coordenador).Handle(Anexar("laudo.pdf", Pdf), TestContext.Current.CancellationToken);

        var d = Assert.Single(_repo.Documentos);
        Assert.Equal(VisibilidadeDocumento.CompartilhadoCoordenacao, d.Visibilidade);
        Assert.Equal(coordenador.Id, d.CompartilhadoPorId);
        Assert.Equal(_curso, d.CursoId);
    }

    [Fact]
    public async Task Compartilhar_exige_justificativa_e_audita()
    {
        var ct = TestContext.Current.CancellationToken;
        var d = Semear();
        var uow = new UnidadeDeTrabalhoFalsa();
        var handler = new CompartilharDocumentoHandler(new CompartilharDocumentoValidator(), _repo, _alunos, Profissional(), new RelogioFixo(),
            _auditoria, uow);

        var erro = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new CompartilharDocumentoCommand(d.Id, " ab ", "v1"), ct));
        Assert.True(erro.Erros.ContainsKey("justificativa"));
        Assert.Equal(VisibilidadeDocumento.RestritoNap, d.Visibilidade);

        await handler.Handle(new CompartilharDocumentoCommand(d.Id, "Necessário para adaptação curricular", "v1"), ct);

        Assert.Equal(VisibilidadeDocumento.CompartilhadoCoordenacao, d.Visibilidade);
        Assert.Equal("Necessário para adaptação curricular", d.CompartilhadoJustificativa);
        var registro = Assert.Single(_auditoria.Registros);
        Assert.Equal(("documento.compartilhar", d.Id), (registro.Acao, registro.RecursoId));
        Assert.Equal(["v1"], uow.VersoesExigidas);
        Assert.Equal(1, uow.Salvamentos);
    }

    [Fact]
    public async Task Alterar_categoria_mantem_visibilidade()
    {
        var d = Semear();
        d.Compartilhar("Necessário para adaptação", Guid.NewGuid(), d.CursoId, DateTimeOffset.UtcNow);
        var handler = new AlterarCategoriaDocumentoHandler(new AlterarCategoriaDocumentoValidator(), _repo, Profissional(),
            _auditoria, new UnidadeDeTrabalhoFalsa());

        await handler.Handle(new AlterarCategoriaDocumentoCommand(d.Id, CategoriaDocumento.Relatorio, "v1"),
            TestContext.Current.CancellationToken);

        Assert.Equal(CategoriaDocumento.Relatorio, d.Categoria);
        Assert.Equal(VisibilidadeDocumento.CompartilhadoCoordenacao, d.Visibilidade);
        Assert.Equal("documento.categoria", Assert.Single(_auditoria.Registros).Acao);
    }

    [Fact]
    public async Task Arquivar_com_versao_antiga_lanca_conflito()
    {
        var d = Semear();
        var uow = new UnidadeDeTrabalhoFalsa { VersaoAtual = "v2" };
        var handler = new ArquivarDocumentoHandler(new ArquivarDocumentoValidator(), _repo, Profissional(), new RelogioFixo(),
            _auditoria, uow);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new ArquivarDocumentoCommand(d.Id, "Documento substituído", "v1"), TestContext.Current.CancellationToken));

        Assert.False(d.Arquivado);
        Assert.Empty(_auditoria.Registros);
        Assert.Equal(0, uow.Salvamentos);
    }

    [Fact]
    public async Task Coordenador_de_curso_recebe_404_em_restrito_e_403_em_compartilhado()
    {
        var ct = TestContext.Current.CancellationToken;
        var restrito = Semear();
        var compartilhado = Semear();
        compartilhado.Compartilhar("Necessário para adaptação", Guid.NewGuid(), compartilhado.CursoId, DateTimeOffset.UtcNow);
        var handler = new RevogarCompartilhamentoDocumentoHandler(new RevogarCompartilhamentoDocumentoValidator(), _repo,
            new UsuarioAtualFalso(Perfis.CoordenadorCurso, _curso), new RelogioFixo(), _auditoria, new UnidadeDeTrabalhoFalsa());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new RevogarCompartilhamentoDocumentoCommand(restrito.Id, "Não é mais necessário", "v1"), ct));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new RevogarCompartilhamentoDocumentoCommand(compartilhado.Id, "Não é mais necessário", "v1"), ct));
        Assert.Equal(VisibilidadeDocumento.CompartilhadoCoordenacao, compartilhado.Visibilidade);
    }

    [Fact]
    public async Task Consulta_e_lista_auditam()
    {
        var ct = TestContext.Current.CancellationToken;
        var d = Semear();
        var consultas = new DocumentoQueriesFalso(_repo);

        await new ObterDocumentoHandler(new ObterDocumentoValidator(), consultas, Profissional(), _auditoria, new UnidadeDeTrabalhoFalsa())
            .Handle(new ObterDocumentoQuery(d.Id), ct);
        await new ListarDocumentosHandler(new ListarDocumentosValidator(), consultas, Profissional(), _auditoria, new UnidadeDeTrabalhoFalsa())
            .Handle(new ListarDocumentosQuery(d.AlunoId), ct);

        Assert.Equal([("documento.consulta", (Guid?)d.Id), ("documento.lista.consulta", (Guid?)d.AlunoId)],
            _auditoria.Registros.Select(r => (r.Acao, r.RecursoId)));
    }

    [Fact]
    public async Task Download_audita()
    {
        var ct = TestContext.Current.CancellationToken;
        var d = Semear(await _armazenamento.SalvarAsync(new MemoryStream(Pdf), ct));
        var uow = new UnidadeDeTrabalhoFalsa(_ordem);
        var handler = new BaixarDocumentoHandler(new BaixarDocumentoValidator(), new DocumentoQueriesFalso(_repo), _armazenamento,
            Profissional(), _auditoria, uow);

        var arquivo = await handler.Handle(new BaixarDocumentoQuery(d.Id), ct);
        await using var conteudo = arquivo.Conteudo;
        using var lido = new MemoryStream();
        await conteudo.CopyToAsync(lido, ct);

        Assert.Equal(Pdf, lido.ToArray());
        Assert.Equal(("laudo.pdf", "application/pdf"), (arquivo.NomeArquivo, arquivo.TipoConteudo));
        var registro = Assert.Single(_auditoria.Registros);
        Assert.Equal(("documento.download", d.Id), (registro.Acao, registro.RecursoId));
        Assert.Equal(["salvar", "abrir"], _ordem);
    }

    [Theory]
    [InlineData(Perfis.Sip)]
    [InlineData(Perfis.Gestao)]
    public async Task Sip_e_gestao_nao_baixam(string perfil)
    {
        var d = Semear();
        var handler = new BaixarDocumentoHandler(new BaixarDocumentoValidator(), new DocumentoQueriesFalso(_repo), _armazenamento,
            new UsuarioAtualFalso(perfil), _auditoria, new UnidadeDeTrabalhoFalsa());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new BaixarDocumentoQuery(d.Id), TestContext.Current.CancellationToken));
        Assert.Empty(_auditoria.Registros);
    }
}
