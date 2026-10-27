using Nap.Application.Comum;
using Nap.Application.Features.Peis.Commands;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Alunos;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Comunicacoes;
using Nap.Domain.Peis;

namespace Nap.Application.Tests;

public class PeisTests
{
    private readonly Guid _curso = Guid.NewGuid();
    private readonly RelogioFixo _relogio = new();
    private readonly AlunoRepositoryEmMemoria _alunos = new();
    private readonly PeiRepositoryEmMemoria _peis = new();
    private readonly AcessoAssinaturaRepositoryEmMemoria _acessos = new();
    private readonly MensagemRepositoryEmMemoria _mensagens = new();
    private readonly UsuarioQueriesFalso _usuarios;
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly CacheConsultasFalso _cache = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new();
    private readonly UsuarioAtualFalso _prof = new(Perfis.ProfissionalNap);
    private readonly UsuarioAtualFalso _coord = new(Perfis.CoordenadorNap);
    private readonly Aluno _aluno;

    public PeisTests()
    {
        _usuarios = new UsuarioQueriesFalso(_prof.Id);
        _usuarios.Usuarios.Add((_prof.Id, Perfis.ProfissionalNap, null, true));
        _usuarios.Nomes[_prof.Id] = "Ana Beatriz";
        _aluno = Aluno.Criar("Maria da Silva", Matricula.Criar("2024001"), _curso, 3, new DateOnly(2026, 8, 1),
            _prof.Id, null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        _alunos.Alunos.Add(_aluno);
    }

    private static ConteudoPeiInput Conteudo() => new("Demanda sigilosa do NAP", "Necessidades", "Adaptações", null, null,
        null, new DateOnly(2026, 8, 1), new DateOnly(2027, 7, 31));

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>PEI do aluno (responsável = _prof) com a v1 levada até o estado pedido pelo próprio domínio.</summary>
    private VersaoPei Semear(EstadoVersaoPei alvo, Guid? alunoId = null)
    {
        var agora = _relogio.Agora.AddDays(-1);
        var pei = Pei.Criar(alunoId ?? _aluno.Id, _prof.Id, Conteudo().ParaDominio(), _prof.Id, agora);
        _peis.Adicionar(pei);
        var v = pei.Versoes.Single();
        Avancar(pei, v, alvo, agora);
        return v;
    }

    private void Avancar(Pei pei, VersaoPei v, EstadoVersaoPei alvo, DateTimeOffset agora)
    {
        if (alvo == EstadoVersaoPei.Rascunho) return;
        v.EnviarParaRevisao(agora);
        if (alvo == EstadoVersaoPei.EmRevisao) return;
        var hash = CalculadoraHashPei.Calcular(v.Conteudo);
        v.AprovarRevisao(_coord.Id, agora);
        if (alvo == EstadoVersaoPei.AguardandoAssinaturas) return;
        v.RegistrarAssinatura(PapelAssinatura.Profissional, _prof.Id, "Ana Beatriz", hash, "e1", agora);
        v.RegistrarAssinatura(PapelAssinatura.Aluno, null, "Maria da Silva", hash, "e2", agora);
        if (alvo == EstadoVersaoPei.Assinada) return;
        pei.Publicar(v, _coord.Id, _curso, agora);
    }

    private Pei PeiDe(VersaoPei v) => _peis.Peis.Single(p => p.Id == v.PeiId);

    private CriarPeiHandler Criar(IUsuarioAtual u) =>
        new(new CriarPeiValidator(), _peis, _alunos, _usuarios, u, _relogio, _auditoria, _uow, _cache);

    private AtualizarConteudoPeiHandler Atualizar(IUsuarioAtual u) =>
        new(new AtualizarConteudoPeiValidator(), _peis, u, _relogio, _auditoria, _uow, _cache);

    private EnviarPeiParaRevisaoHandler Enviar(IUsuarioAtual u) =>
        new(new EnviarPeiParaRevisaoValidator(), _peis, u, _relogio, _auditoria, _uow, _cache);

    private AprovarRevisaoPeiHandler Aprovar(IUsuarioAtual u) =>
        new(new AprovarRevisaoPeiValidator(), _peis, u, _relogio, _auditoria, _uow, _cache);

    private DevolverPeiHandler Devolver(IUsuarioAtual u) =>
        new(new DevolverPeiValidator(), _peis, _acessos, u, _relogio, _auditoria, _uow, _cache);

    private AssinarPeiProfissionalHandler Assinar(IUsuarioAtual u) =>
        new(new AssinarPeiProfissionalValidator(), _peis, new ServicoAssinaturaFalso(), _usuarios, u, _relogio, _auditoria,
            _uow, _cache);

    private GerarAcessoAssinaturaAlunoHandler GerarAcesso(IUsuarioAtual u) =>
        new(new GerarAcessoAssinaturaAlunoValidator(), _peis, _acessos, u, _relogio, _auditoria, _uow, _cache);

    private PublicarPeiHandler Publicar(IUsuarioAtual u) =>
        new(new PublicarPeiValidator(), _peis, _alunos, _usuarios, _mensagens, u, _relogio, _auditoria, _uow, _cache);

    private RegistrarCienciaPeiHandler Ciencia(IUsuarioAtual u) =>
        new(new RegistrarCienciaPeiValidator(), _peis, _alunos, u, _relogio, _auditoria, _uow, _cache);

    private CriarNovaVersaoPeiHandler NovaVersao(IUsuarioAtual u) =>
        new(new CriarNovaVersaoPeiValidator(), _peis, u, _relogio, _auditoria, _uow, _cache);

    private DescartarVersaoPeiHandler Descartar(IUsuarioAtual u) =>
        new(new DescartarVersaoPeiValidator(), _peis, u, _auditoria, _uow, _cache);

    [Fact]
    public async Task Profissional_cria_e_envia_para_revisao()
    {
        var versaoId = await Criar(_prof).Handle(new CriarPeiCommand(_aluno.Id, _prof.Id, Conteudo()), Ct);

        var pei = Assert.Single(_peis.Peis);
        var v = Assert.Single(pei.Versoes);
        Assert.Equal((versaoId, 1, EstadoVersaoPei.Rascunho), (v.Id, v.Numero, v.Estado));
        Assert.Equal((_aluno.Id, _prof.Id), (pei.AlunoId, pei.ProfissionalResponsavelId));
        Assert.Equal("Demanda sigilosa do NAP", v.Conteudo.DemandaInterna);

        await Enviar(_prof).Handle(new EnviarPeiParaRevisaoCommand(versaoId, "v1"), Ct);

        Assert.Equal(EstadoVersaoPei.EmRevisao, v.Estado);
        Assert.Equal(["v1"], _uow.VersoesExigidas);
        Assert.Equal([("pei.criar", "Pei", (Guid?)pei.Id), ("pei.revisao.enviar", "VersaoPei", versaoId)], _auditoria.Registros);
        Assert.Equal(2, _cache.Invalidacoes);
    }

    [Fact]
    public async Task Criar_exige_responsavel_profissional_ativo_e_aluno_existente()
    {
        var semAluno = await Assert.ThrowsAsync<ValidationException>(() =>
            Criar(_prof).Handle(new CriarPeiCommand(Guid.NewGuid(), _prof.Id, Conteudo()), Ct));
        Assert.True(semAluno.Erros.ContainsKey("alunoId"));

        var inativo = await Assert.ThrowsAsync<ValidationException>(() =>
            Criar(_prof).Handle(new CriarPeiCommand(_aluno.Id, Guid.NewGuid(), Conteudo()), Ct));
        Assert.True(inativo.Erros.ContainsKey("profissionalResponsavelId"));
        Assert.Empty(_peis.Peis);
    }

    [Fact]
    public async Task Profissional_nao_aprova_nem_publica()
    {
        var emRevisao = Semear(EstadoVersaoPei.EmRevisao);
        var assinada = Semear(EstadoVersaoPei.Assinada);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Aprovar(_prof).Handle(new AprovarRevisaoPeiCommand(emRevisao.Id, "v"), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Devolver(_prof).Handle(new DevolverPeiCommand(emRevisao.Id, "Ajustar adaptações", "v"), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Publicar(_prof).Handle(new PublicarPeiCommand(assinada.Id, "v"), Ct));

        Assert.Equal(EstadoVersaoPei.EmRevisao, emRevisao.Estado);
        Assert.Equal(EstadoVersaoPei.Assinada, assinada.Estado);
        Assert.Equal(0, _uow.Salvamentos);
    }

    [Fact]
    public async Task Coordenacao_aprova_e_congela_hash()
    {
        var v = Semear(EstadoVersaoPei.EmRevisao);

        await Aprovar(_coord).Handle(new AprovarRevisaoPeiCommand(v.Id, "v"), Ct);

        Assert.Equal(EstadoVersaoPei.AguardandoAssinaturas, v.Estado);
        Assert.Equal(CalculadoraHashPei.Calcular(v.Conteudo), v.HashConteudo);
        Assert.Equal(_coord.Id, v.AprovadaPorId);
        Assert.Equal("pei.revisao.aprovar", Assert.Single(_auditoria.Registros).Acao);
        await Assert.ThrowsAsync<DomainException>(() =>
            Atualizar(_prof).Handle(new AtualizarConteudoPeiCommand(v.Id, Conteudo(), "v"), Ct));
    }

    [Fact]
    public async Task Devolver_exige_justificativa()
    {
        var v = Semear(EstadoVersaoPei.AguardandoAssinaturas);
        await GerarAcesso(_prof).Handle(new GerarAcessoAssinaturaAlunoCommand(v.Id), Ct);

        var erro = await Assert.ThrowsAsync<ValidationException>(() =>
            Devolver(_coord).Handle(new DevolverPeiCommand(v.Id, " ab ", "v"), Ct));
        Assert.True(erro.Erros.ContainsKey("justificativa"));
        Assert.Equal(EstadoVersaoPei.AguardandoAssinaturas, v.Estado);

        await Devolver(_coord).Handle(new DevolverPeiCommand(v.Id, "Rever as adaptações", "v"), Ct);

        Assert.Equal(EstadoVersaoPei.Rascunho, v.Estado);
        Assert.Equal("Rever as adaptações", v.JustificativaDevolucao);
        Assert.False(Assert.Single(_acessos.Acessos).Valido(_relogio.Agora));
    }

    [Fact]
    public async Task So_o_responsavel_assina_como_profissional()
    {
        var v = Semear(EstadoVersaoPei.AguardandoAssinaturas);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Assinar(_coord).Handle(new AssinarPeiProfissionalCommand(v.Id, "v"), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Assinar(new UsuarioAtualFalso(Perfis.ProfissionalNap)).Handle(new AssinarPeiProfissionalCommand(v.Id, "v"), Ct));
        Assert.Empty(v.Assinaturas);

        await Assinar(_prof).Handle(new AssinarPeiProfissionalCommand(v.Id, "v"), Ct);

        var a = Assert.Single(v.Assinaturas);
        Assert.Equal((PapelAssinatura.Profissional, (Guid?)_prof.Id, "Ana Beatriz"), (a.Papel, a.SignatarioId, a.NomeSignatario));
        Assert.Equal($"evidencia-Profissional-{v.HashConteudo}", a.Evidencia);
        Assert.Equal(("pei.assinar", "AssinaturaPei", (Guid?)a.Id), Assert.Single(_auditoria.Registros));
    }

    private TrocarResponsavelPeiHandler Trocar(IUsuarioAtual u, UsuarioQueriesFalso? usuarios = null) =>
        new(new TrocarResponsavelPeiValidator(), _peis, usuarios ?? _usuarios, u, _relogio, _auditoria, _uow, _cache);

    [Fact]
    public async Task Trocar_responsavel_exige_profissional_ativo_e_justificativa()
    {
        var v = Semear(EstadoVersaoPei.AguardandoAssinaturas);
        var novo = Guid.NewGuid();
        var usuarios = new UsuarioQueriesFalso(_prof.Id, novo);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Trocar(_prof, usuarios).Handle(new TrocarResponsavelPeiCommand(v.PeiId, novo, "Redistribuição da equipe"), Ct));
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Trocar(_coord, usuarios).Handle(new TrocarResponsavelPeiCommand(v.PeiId, novo, " x "), Ct));
        Assert.Contains("justificativa", ex.Erros.Keys);
        ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Trocar(_coord, usuarios).Handle(new TrocarResponsavelPeiCommand(v.PeiId, Guid.NewGuid(), "Redistribuição da equipe"), Ct));
        Assert.Contains("novoResponsavelId", ex.Erros.Keys);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Trocar(_coord, usuarios).Handle(new TrocarResponsavelPeiCommand(Guid.NewGuid(), novo, "Redistribuição da equipe"), Ct));
        Assert.Equal(_prof.Id, PeiDe(v).ProfissionalResponsavelId);
        Assert.Equal(0, _uow.Salvamentos);

        await Trocar(_coord, usuarios).Handle(new TrocarResponsavelPeiCommand(v.PeiId, novo, "Redistribuição da equipe"), Ct);
        Assert.Equal(novo, PeiDe(v).ProfissionalResponsavelId);
        Assert.Equal(new TrocaResponsavelPei(_prof.Id, novo, "Redistribuição da equipe", _coord.Id, _relogio.Agora),
            Assert.Single(PeiDe(v).TrocasResponsavel));
        Assert.Equal(("pei.responsavel.trocar", "Pei", (Guid?)v.PeiId), Assert.Single(_auditoria.Registros));
        Assert.Equal(1, _cache.Invalidacoes);
    }

    [Fact]
    public async Task Novo_responsavel_assina_como_profissional()
    {
        var v = Semear(EstadoVersaoPei.AguardandoAssinaturas);
        var novo = new UsuarioAtualFalso(Perfis.ProfissionalNap);
        _usuarios.Usuarios.Add((novo.Id, Perfis.ProfissionalNap, null, true));
        _usuarios.Nomes[novo.Id] = "Carlos Mendes";
        await Trocar(_coord, new UsuarioQueriesFalso(_prof.Id, novo.Id))
            .Handle(new TrocarResponsavelPeiCommand(v.PeiId, novo.Id, "Ana Beatriz entrou de licença"), Ct);

        await Assert.ThrowsAsync<ForbiddenException>(() => Assinar(_prof).Handle(new AssinarPeiProfissionalCommand(v.Id, "v"), Ct));
        await Assinar(novo).Handle(new AssinarPeiProfissionalCommand(v.Id, "v"), Ct);

        var a = Assert.Single(v.Assinaturas);
        Assert.Equal((PapelAssinatura.Profissional, (Guid?)novo.Id, "Carlos Mendes"), (a.Papel, a.SignatarioId, a.NomeSignatario));
    }

    [Fact]
    public async Task Publicar_sem_assinatura_do_aluno_e_422()
    {
        var v = Semear(EstadoVersaoPei.AguardandoAssinaturas);
        await Assinar(_prof).Handle(new AssinarPeiProfissionalCommand(v.Id, "v"), Ct);

        await Assert.ThrowsAsync<DomainException>(() => Publicar(_coord).Handle(new PublicarPeiCommand(v.Id, "v"), Ct));

        Assert.Equal(EstadoVersaoPei.AguardandoAssinaturas, v.Estado);
        Assert.Empty(_mensagens.Mensagens);
    }

    [Fact]
    public async Task Publicar_notifica_coordenadores_ativos_do_curso()
    {
        Guid c1 = Guid.NewGuid(), c2 = Guid.NewGuid();
        _usuarios.Usuarios.Add((c1, Perfis.CoordenadorCurso, _curso, true));
        _usuarios.Usuarios.Add((c2, Perfis.CoordenadorCurso, _curso, true));
        _usuarios.Usuarios.Add((Guid.NewGuid(), Perfis.CoordenadorCurso, _curso, false));
        _usuarios.Usuarios.Add((Guid.NewGuid(), Perfis.CoordenadorCurso, Guid.NewGuid(), true));
        var v = Semear(EstadoVersaoPei.Assinada);

        await Publicar(_coord).Handle(new PublicarPeiCommand(v.Id, "v"), Ct);

        Assert.Equal(EstadoVersaoPei.Publicada, v.Estado);
        Assert.Equal((_curso, (Guid?)_coord.Id, (DateTimeOffset?)_relogio.Agora), (v.CursoPublicacaoId!.Value, v.PublicadaPorId, v.PublicadaEm));
        Assert.Equal(new[] { c1, c2 }.Order(), _mensagens.Mensagens.Select(m => m.DestinatarioId!.Value).Order());
        Assert.All(_mensagens.Mensagens, m =>
        {
            Assert.Equal((TipoMensagem.EnvioPei, EstadoMensagem.Enviada), (m.Tipo, m.Estado));
            Assert.Equal((_coord.Id, (Guid?)_curso, (Guid?)_aluno.Id, (Guid?)v.Id), (m.AutorId, m.CursoId, m.AlunoId, m.PeiVersaoId));
            Assert.Equal("PEI publicado: Maria da Silva (versão 1)", m.Assunto);
            Assert.Equal("O PEI de Maria da Silva, vigente de 01/08/2026 a 31/07/2027, foi publicado. "
                + "Abra a ficha do aluno, aba PEI, para ler e registrar ciência.", m.Corpo);
            Assert.DoesNotContain("Demanda", m.Corpo);
        });
        Assert.Equal(1, _uow.Salvamentos);
        Assert.Equal("pei.publicar", Assert.Single(_auditoria.Registros).Acao);
    }

    [Fact]
    public async Task Publicar_assunto_longo_e_cortado_no_limite()
    {
        _usuarios.Usuarios.Add((Guid.NewGuid(), Perfis.CoordenadorCurso, _curso, true));
        // O emoji cai exatamente no 200º caractere do assunto: o corte não pode deixar meio par substituto.
        var aluno = Aluno.Criar(new string('a', 184) + "😀" + new string('b', 14), Matricula.Criar("2024002"), _curso, 3,
            new DateOnly(2026, 8, 1), _prof.Id, null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        _alunos.Alunos.Add(aluno);
        var v = Semear(EstadoVersaoPei.Assinada, aluno.Id);

        await Publicar(_coord).Handle(new PublicarPeiCommand(v.Id, "v"), Ct);

        var m = Assert.Single(_mensagens.Mensagens);
        Assert.Equal(199, m.Assunto.Length);
        Assert.False(char.IsHighSurrogate(m.Assunto[^1]));
    }

    [Fact]
    public async Task Publicar_v2_arquiva_v1()
    {
        var v1 = Semear(EstadoVersaoPei.Publicada);
        var pei = PeiDe(v1);
        var v2Id = await NovaVersao(_prof).Handle(new CriarNovaVersaoPeiCommand(pei.Id), Ct);
        var v2 = pei.Versoes.Single(v => v.Id == v2Id);
        Assert.Equal((2, EstadoVersaoPei.Rascunho, _prof.Id), (v2.Numero, v2.Estado, v2.CriadaPorId));
        Avancar(pei, v2, EstadoVersaoPei.Assinada, _relogio.Agora);

        await Publicar(_coord).Handle(new PublicarPeiCommand(v2.Id, "v"), Ct);

        Assert.Equal(EstadoVersaoPei.Publicada, v2.Estado);
        Assert.Equal((EstadoVersaoPei.Arquivada, "Substituída pela versão 2"), (v1.Estado, v1.MotivoArquivamento));
    }

    [Fact]
    public async Task Publicar_sem_coordenador_ativo_publica_mesmo_assim()
    {
        _usuarios.Usuarios.Add((Guid.NewGuid(), Perfis.CoordenadorCurso, _curso, false));
        var v = Semear(EstadoVersaoPei.Assinada);

        await Publicar(_coord).Handle(new PublicarPeiCommand(v.Id, "v"), Ct);

        Assert.Equal(EstadoVersaoPei.Publicada, v.Estado);
        Assert.Empty(_mensagens.Mensagens);
        Assert.Equal(1, _uow.Salvamentos);
    }

    [Fact]
    public async Task Arquivar_so_publicada_pela_coordenacao()
    {
        var v = Semear(EstadoVersaoPei.Publicada);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ArquivarPeiHandler(new ArquivarPeiValidator(), _peis, _prof, _relogio, _auditoria, _uow, _cache)
                .Handle(new ArquivarPeiCommand(v.Id, "Fim do semestre", "v"), Ct));

        await new ArquivarPeiHandler(new ArquivarPeiValidator(), _peis, _coord, _relogio, _auditoria, _uow, _cache)
            .Handle(new ArquivarPeiCommand(v.Id, "Fim do semestre", "v"), Ct);

        Assert.Equal((EstadoVersaoPei.Arquivada, "Fim do semestre"), (v.Estado, v.MotivoArquivamento));
    }

    [Fact]
    public async Task Ciencia_de_coordenador_de_outro_curso_e_404()
    {
        var publicada = Semear(EstadoVersaoPei.Publicada);
        var rascunho = Semear(EstadoVersaoPei.Rascunho);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Ciencia(new UsuarioAtualFalso(Perfis.CoordenadorCurso, Guid.NewGuid()))
                .Handle(new RegistrarCienciaPeiCommand(publicada.Id), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Ciencia(new UsuarioAtualFalso(Perfis.CoordenadorCurso, _curso)).Handle(new RegistrarCienciaPeiCommand(rascunho.Id), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Ciencia(_coord).Handle(new RegistrarCienciaPeiCommand(publicada.Id), Ct));
        Assert.Empty(publicada.Ciencias);
    }

    [Fact]
    public async Task Ciencia_do_coordenador_do_curso_atual_apos_transferencia()
    {
        var publicada = Semear(EstadoVersaoPei.Publicada);
        var cursoNovo = Guid.NewGuid();
        _aluno.TransferirCurso(cursoNovo, new DateOnly(2026, 9, 1), "Mudou de curso", _prof.Id, DateTimeOffset.UtcNow);

        await Ciencia(new UsuarioAtualFalso(Perfis.CoordenadorCurso, cursoNovo)).Handle(new RegistrarCienciaPeiCommand(publicada.Id), Ct);

        Assert.Single(publicada.Ciencias);
    }

    [Fact]
    public async Task Ciencia_repetida_nao_duplica()
    {
        var v = Semear(EstadoVersaoPei.Publicada);
        var coordCurso = new UsuarioAtualFalso(Perfis.CoordenadorCurso, _curso);

        await Ciencia(coordCurso).Handle(new RegistrarCienciaPeiCommand(v.Id), Ct);
        await Ciencia(coordCurso).Handle(new RegistrarCienciaPeiCommand(v.Id), Ct);

        var c = Assert.Single(v.Ciencias);
        Assert.Equal((coordCurso.Id, _relogio.Agora), (c.CoordenadorId, c.Em));
        Assert.Equal("pei.ciencia", Assert.Single(_auditoria.Registros).Acao);
    }

    [Fact]
    public async Task Ciencia_nao_engole_conflito_de_versao()
    {
        var v = Semear(EstadoVersaoPei.Publicada);
        var uow = new UnidadeDeTrabalhoFalsa { ConflitoAoSalvar = "O registro foi alterado por outra pessoa. Recarregue e tente novamente" };
        var handler = new RegistrarCienciaPeiHandler(new RegistrarCienciaPeiValidator(), _peis, _alunos,
            new UsuarioAtualFalso(Perfis.CoordenadorCurso, _curso), _relogio, _auditoria, uow, _cache);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new RegistrarCienciaPeiCommand(v.Id), Ct));

        // Já a unicidade (outra requisição do mesmo coordenador gravou antes) é sucesso.
        var duplicada = new UnidadeDeTrabalhoFalsa { ConflitoAoSalvar = RegistrarCienciaPeiHandler.MensagemCienciaDuplicada };
        await new RegistrarCienciaPeiHandler(new RegistrarCienciaPeiValidator(), _peis, _alunos,
            new UsuarioAtualFalso(Perfis.CoordenadorCurso, _curso), _relogio, _auditoria, duplicada, _cache)
            .Handle(new RegistrarCienciaPeiCommand(Semear(EstadoVersaoPei.Publicada).Id), Ct);
    }

    [Fact]
    public async Task Gerar_acesso_invalida_o_anterior()
    {
        var v = Semear(EstadoVersaoPei.AguardandoAssinaturas);

        var primeiro = await GerarAcesso(_prof).Handle(new GerarAcessoAssinaturaAlunoCommand(v.Id), Ct);
        var segundo = await GerarAcesso(_prof).Handle(new GerarAcessoAssinaturaAlunoCommand(v.Id), Ct);

        var anterior = _acessos.Acessos.Single(a => a.Id == primeiro.AcessoId);
        var atual = _acessos.Acessos.Single(a => a.Id == segundo.AcessoId);
        Assert.False(anterior.Valido(_relogio.Agora));
        Assert.True(atual.Valido(_relogio.Agora));
        Assert.Equal(v.HashConteudo, atual.HashConteudo);
        Assert.Equal(AcessoAssinaturaPei.HashDe(segundo.Token), atual.TokenHash);
        Assert.Matches("^[0-9]{6}$", segundo.Codigo);
        Assert.Equal(_relogio.Agora.AddHours(72), segundo.ExpiraEm);
        Assert.All(_auditoria.Registros, r => Assert.Equal(("pei.acesso.gerar", "AcessoAssinaturaPei"), (r.Acao, r.Recurso)));
        Assert.Equal([primeiro.AcessoId, segundo.AcessoId], _auditoria.Registros.Select(r => r.RecursoId!.Value));
    }

    [Fact]
    public async Task Gerar_acesso_exige_aguardando_assinatura_do_aluno()
    {
        var rascunho = Semear(EstadoVersaoPei.Rascunho);
        var assinada = Semear(EstadoVersaoPei.Assinada);

        await Assert.ThrowsAsync<DomainException>(() =>
            GerarAcesso(_prof).Handle(new GerarAcessoAssinaturaAlunoCommand(rascunho.Id), Ct));
        await Assert.ThrowsAsync<DomainException>(() =>
            GerarAcesso(_prof).Handle(new GerarAcessoAssinaturaAlunoCommand(assinada.Id), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            GerarAcesso(new UsuarioAtualFalso(Perfis.CoordenadorCurso, _curso))
                .Handle(new GerarAcessoAssinaturaAlunoCommand(Semear(EstadoVersaoPei.Publicada).Id), Ct));
        Assert.Empty(_acessos.Acessos);
    }

    [Fact]
    public async Task Alterar_publicada_e_422()
    {
        var v = Semear(EstadoVersaoPei.Publicada);
        var antes = v.Conteudo;

        await Assert.ThrowsAsync<DomainException>(() =>
            Atualizar(_prof).Handle(new AtualizarConteudoPeiCommand(v.Id, Conteudo() with { Adaptacoes = "Outra" }, "v"), Ct));

        Assert.Same(antes, v.Conteudo);
        Assert.Equal(0, _uow.Salvamentos);
    }

    [Fact]
    public async Task Atualizar_rascunho_troca_o_conteudo()
    {
        var v = Semear(EstadoVersaoPei.Rascunho);

        await Atualizar(_prof).Handle(new AtualizarConteudoPeiCommand(v.Id, Conteudo() with { Adaptacoes = "Outra" }, "v7"), Ct);

        Assert.Equal("Outra", v.Conteudo.Adaptacoes);
        Assert.Equal(["v7"], _uow.VersoesExigidas);
    }

    [Fact]
    public async Task Nova_versao_com_versao_aberta_e_conflito()
    {
        var v = Semear(EstadoVersaoPei.EmRevisao);

        var erro = await Assert.ThrowsAsync<ConflictException>(() =>
            NovaVersao(_prof).Handle(new CriarNovaVersaoPeiCommand(v.PeiId), Ct));

        Assert.Equal("Já existe uma versão em elaboração.", erro.Message);
        Assert.Single(PeiDe(v).Versoes);
    }

    [Fact]
    public async Task Nova_versao_com_versao_assinada_e_conflito()
    {
        var v = Semear(EstadoVersaoPei.Assinada);

        var erro = await Assert.ThrowsAsync<ConflictException>(() =>
            NovaVersao(_prof).Handle(new CriarNovaVersaoPeiCommand(v.PeiId), Ct));

        Assert.Equal(CriarNovaVersaoPeiHandler.MensagemVersaoAssinada, erro.Message);
        Assert.Single(PeiDe(v).Versoes);
    }

    [Fact]
    public async Task Descartar_so_rascunho_a_partir_da_v2()
    {
        var v1 = Semear(EstadoVersaoPei.Publicada);
        var pei = PeiDe(v1);
        var v2Id = await NovaVersao(_prof).Handle(new CriarNovaVersaoPeiCommand(pei.Id), Ct);
        var v2 = pei.Versoes.Single(v => v.Id == v2Id);

        await Assert.ThrowsAsync<DomainException>(() => Descartar(_prof).Handle(new DescartarVersaoPeiCommand(v1.Id, "v1"), Ct));
        await Descartar(_prof).Handle(new DescartarVersaoPeiCommand(v2Id, "v1"), Ct);

        Assert.Equal([v1], pei.Versoes);
        Assert.Equal([v2], _peis.VersoesRemovidas);
    }

    [Theory]
    [InlineData(Perfis.Sip)]
    [InlineData(Perfis.Gestao)]
    public async Task Sip_e_gestao_recebem_403(string perfil)
    {
        var u = new UsuarioAtualFalso(perfil, _curso);
        var publicada = Semear(EstadoVersaoPei.Publicada);
        var rascunho = Semear(EstadoVersaoPei.Rascunho);

        await Assert.ThrowsAsync<ForbiddenException>(() => Criar(u).Handle(new CriarPeiCommand(_aluno.Id, _prof.Id, Conteudo()), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Atualizar(u).Handle(new AtualizarConteudoPeiCommand(rascunho.Id, Conteudo(), "v"), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => Ciencia(u).Handle(new RegistrarCienciaPeiCommand(publicada.Id), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => Publicar(u).Handle(new PublicarPeiCommand(publicada.Id, "v"), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            NovaVersao(u).Handle(new CriarNovaVersaoPeiCommand(publicada.PeiId), Ct));
        Assert.Equal(0, _uow.Salvamentos);
    }
}
