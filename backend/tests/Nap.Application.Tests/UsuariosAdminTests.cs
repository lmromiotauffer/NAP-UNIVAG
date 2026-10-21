using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;
using Nap.Application.Features.Usuarios.Commands;
using Nap.Application.Features.Usuarios.Queries;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Comum;

namespace Nap.Application.Tests;

public class UsuariosAdminTests
{
    private readonly Guid _cursoAtivo = Guid.NewGuid();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly CacheConsultasFalso _cache = new();
    private readonly GestaoUsuariosFalsa _gestao;

    public UsuariosAdminTests() => _gestao = new(_auditoria);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private CriarUsuarioHandler Criar(IUsuarioAtual u) =>
        new(new CriarUsuarioValidator(), _gestao, new CursoQueriesFalso(_cursoAtivo), u, _auditoria, _cache);

    private AtualizarUsuarioHandler Atualizar(IUsuarioAtual u) =>
        new(new AtualizarUsuarioValidator(), _gestao, new CursoQueriesFalso(_cursoAtivo), u, _auditoria, _cache);

    private AlterarSituacaoUsuarioHandler Situacao(IUsuarioAtual u) =>
        new(new AlterarSituacaoUsuarioValidator(), _gestao, u, _auditoria, _cache);

    private RedefinirSenhaUsuarioHandler Redefinir(IUsuarioAtual u) =>
        new(new RedefinirSenhaUsuarioValidator(), _gestao, u, _auditoria);

    private static CriarUsuarioCommand NovoComando(string perfil, Guid? curso = null) =>
        new($"{Guid.NewGuid():N}@teste.nap", "Pessoa Nova", perfil, curso);

    private static AtualizarUsuarioCommand Edicao(UsuarioAdminDto alvo, string? perfil = null, Guid? curso = null, string nome = "Nome Editado") =>
        new(alvo.Id, nome, perfil ?? alvo.Perfil, curso ?? alvo.CursoId, alvo.Versao);

    [Fact]
    public async Task Criar_retorna_senha_temporaria_e_audita_antes_da_porta()
    {
        var dto = await Criar(new UsuarioAtualFalso(Perfis.CoordenadorNap)).Handle(NovoComando(Perfis.Sip), Ct);

        Assert.Equal(16, dto.SenhaTemporaria.Length);
        Assert.Equal(_gestao.UltimaSenha, dto.SenhaTemporaria);
        Assert.True(_gestao.AuditoriaAntesDaPorta);
        Assert.Equal(("usuario.criar", "Usuario", (Guid?)dto.UsuarioId), Assert.Single(_auditoria.Registros));
        Assert.Equal(1, _cache.Invalidacoes);
    }

    [Fact]
    public async Task Redefinir_senha_passa_a_versao_lida_para_a_porta()
    {
        var alvo = _gestao.Adicionar(Perfis.ProfissionalNap);

        var dto = await Redefinir(new UsuarioAtualFalso(Perfis.CoordenadorNap)).Handle(new(alvo.Id), Ct);

        // A porta reconfere a versão: alçada checada sobre o alvo lido continua valendo na gravação.
        Assert.Equal(alvo.Versao, _gestao.UltimaVersao);
        Assert.Equal(_gestao.UltimaSenha, dto.SenhaTemporaria);
        Assert.True(_gestao.AuditoriaAntesDaPorta);
    }

    [Fact]
    public async Task Coordenacao_nap_nao_gerencia_administrador()
    {
        var coordenacao = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        var admin = _gestao.Adicionar(Perfis.Administrador);
        _gestao.Adicionar(Perfis.Administrador);

        await Assert.ThrowsAsync<ForbiddenException>(() => Criar(coordenacao).Handle(NovoComando(Perfis.Administrador), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => Atualizar(coordenacao).Handle(Edicao(admin), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => Situacao(coordenacao).Handle(new(admin.Id, false, admin.Versao), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => Redefinir(coordenacao).Handle(new(admin.Id), Ct));
        Assert.Empty(_gestao.Chamadas);
        Assert.Empty(_auditoria.Registros);
    }

    [Fact]
    public async Task Coordenacao_nap_nao_atribui_perfil_administrador()
    {
        var coordenacao = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        var profissional = _gestao.Adicionar(Perfis.ProfissionalNap);

        await Assert.ThrowsAsync<ForbiddenException>(() => Atualizar(coordenacao).Handle(Edicao(profissional, Perfis.Administrador), Ct));
        Assert.Empty(_gestao.Chamadas);

        // Administrador pode promover.
        await Atualizar(new UsuarioAtualFalso(Perfis.Administrador)).Handle(Edicao(profissional, Perfis.Administrador), Ct);
        Assert.Equal(Perfis.Administrador, _gestao.Usuarios[profissional.Id].Perfil);
    }

    [Fact]
    public async Task Ninguem_altera_o_proprio_acesso()
    {
        var eu = _gestao.Adicionar(Perfis.CoordenadorNap);
        var atual = new UsuarioAtualFalso(Perfis.CoordenadorNap) { Id = eu.Id };

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => Atualizar(atual).Handle(Edicao(eu, Perfis.ProfissionalNap), Ct));
        Assert.Equal("Você não pode alterar o próprio acesso.", ex.Message);
        await Assert.ThrowsAsync<ForbiddenException>(() => Situacao(atual).Handle(new(eu.Id, false, eu.Versao), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => Redefinir(atual).Handle(new(eu.Id), Ct));
        Assert.Empty(_gestao.Chamadas);

        // O próprio nome pode.
        await Atualizar(atual).Handle(Edicao(eu, nome: "Meu Nome Novo"), Ct);
        Assert.Equal("Meu Nome Novo", _gestao.Usuarios[eu.Id].Nome);
    }

    [Fact]
    public async Task Ultimo_administrador_ativo_nao_e_inativado_nem_rebaixado()
    {
        var admin = new UsuarioAtualFalso(Perfis.Administrador);
        var unico = _gestao.Adicionar(Perfis.Administrador);
        _gestao.Adicionar(Perfis.Administrador, ativo: false);

        await Assert.ThrowsAsync<Nap.Domain.Comum.DomainException>(() => Situacao(admin).Handle(new(unico.Id, false, unico.Versao), Ct));
        await Assert.ThrowsAsync<Nap.Domain.Comum.DomainException>(() => Atualizar(admin).Handle(Edicao(unico, Perfis.CoordenadorNap), Ct));
        Assert.Empty(_gestao.Chamadas);

        // Com outro administrador ativo, pode.
        _gestao.Adicionar(Perfis.Administrador);
        await Situacao(admin).Handle(new(unico.Id, false, unico.Versao), Ct);
        Assert.False(_gestao.Usuarios[unico.Id].Ativo);
    }

    [Fact]
    public async Task Coordenador_de_curso_exige_curso_ativo()
    {
        var coordenacao = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        var h = Criar(coordenacao);

        var semCurso = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(NovoComando(Perfis.CoordenadorCurso), Ct));
        Assert.Contains("cursoId", semCurso.Erros.Keys);
        var inativo = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(NovoComando(Perfis.CoordenadorCurso, Guid.NewGuid()), Ct));
        Assert.Contains("cursoId", inativo.Erros.Keys);
        var profissional = _gestao.Adicionar(Perfis.ProfissionalNap);
        await Assert.ThrowsAsync<ValidationException>(() => Atualizar(coordenacao).Handle(Edicao(profissional, Perfis.CoordenadorCurso, Guid.NewGuid()), Ct));
        Assert.Empty(_gestao.Chamadas);

        await h.Handle(NovoComando(Perfis.CoordenadorCurso, _cursoAtivo), Ct);
        // Coordenador de um curso que foi inativado continua editável sem trocar de curso.
        var antigo = _gestao.Adicionar(Perfis.CoordenadorCurso, Guid.NewGuid());
        await Atualizar(coordenacao).Handle(Edicao(antigo, nome: "Outro Nome"), Ct);
        Assert.Equal(["criar", "atualizar"], _gestao.Chamadas);
    }

    [Fact]
    public async Task Outros_perfis_nao_tem_curso()
    {
        var coordenacao = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Criar(coordenacao).Handle(NovoComando(Perfis.Sip, _cursoAtivo), Ct));
        Assert.Contains("cursoId", ex.Erros.Keys);
        var coordCurso = _gestao.Adicionar(Perfis.CoordenadorCurso, _cursoAtivo);
        await Assert.ThrowsAsync<ValidationException>(() => Atualizar(coordenacao).Handle(Edicao(coordCurso, Perfis.Gestao), Ct));
        Assert.Empty(_gestao.Chamadas);
    }

    [Theory]
    [InlineData(Perfis.Sip)]
    [InlineData(Perfis.ProfissionalNap)]
    [InlineData(Perfis.Gestao)]
    [InlineData(Perfis.CoordenadorCurso)]
    public async Task Sip_profissional_gestao_coordenador_curso_recebem_403(string perfil)
    {
        var u = new UsuarioAtualFalso(perfil);
        var alvo = _gestao.Adicionar(Perfis.Sip);
        var eu = _gestao.Adicionar(perfil, perfil == Perfis.CoordenadorCurso ? _cursoAtivo : null, id: u.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => Criar(u).Handle(NovoComando(Perfis.Sip), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => Atualizar(u).Handle(Edicao(alvo), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => Atualizar(u).Handle(Edicao(eu, nome: "Meu Nome"), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => Situacao(u).Handle(new(alvo.Id, false, alvo.Versao), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => Redefinir(u).Handle(new(alvo.Id), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ListarUsuariosHandler(new ListarUsuariosValidator(), _gestao, u).Handle(new ListarUsuariosQuery(), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => new ObterUsuarioHandler(_gestao, u).Handle(new ObterUsuarioQuery(alvo.Id), Ct));
        Assert.Empty(_gestao.Chamadas);
    }

    [Fact]
    public async Task Usuario_inexistente_retorna_404()
    {
        var admin = new UsuarioAtualFalso(Perfis.Administrador);
        await Assert.ThrowsAsync<NotFoundException>(() => Situacao(admin).Handle(new(Guid.NewGuid(), false, "v1"), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => new ObterUsuarioHandler(_gestao, admin).Handle(new ObterUsuarioQuery(Guid.NewGuid()), Ct));
    }
}
