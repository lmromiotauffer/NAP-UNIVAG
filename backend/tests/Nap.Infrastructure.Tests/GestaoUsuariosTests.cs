using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Nap.Application.Comum;
using Nap.Application.Features.Auth;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comum;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Tests;

public class GestaoUsuariosTests(SqlServerFixture sql, RedisFixture redis) : IAsyncDisposable
{
    private readonly ServiceProvider _sp = RedisFixture.CriarProvedor(sql, redis);

    public ValueTask DisposeAsync() => _sp.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Carimbo(SessaoEmitida s) => new JsonWebTokenHandler().ReadJsonWebToken(s.AccessToken).GetClaim("stamp").Value;

    private static Guid Sessao(SessaoEmitida s) => Guid.Parse(new JsonWebTokenHandler().ReadJsonWebToken(s.AccessToken).GetClaim("sid").Value);

    /// <summary>Cria pela porta (com a auditoria antes, como os handlers) e devolve id, e-mail e senha temporária.</summary>
    private async Task<(Guid Id, string Email, string Senha)> CriarAsync(string perfil = Perfis.ProfissionalNap, string? email = null)
    {
        await using var escopo = _sp.CreateAsyncScope();
        var novo = new NovoUsuario(Guid.CreateVersion7(), email ?? $"{Guid.NewGuid():N}@teste.nap", "Usuária administrada", perfil, null);
        var senha = GeradorSenhaTemporaria.Gerar();
        escopo.ServiceProvider.GetRequiredService<IAuditoria>().Registrar("usuario.criar", "Usuario", novo.Id);
        await escopo.ServiceProvider.GetRequiredService<IGestaoUsuarios>().CriarAsync(novo, senha, Ct);
        return (novo.Id, novo.Email, senha);
    }

    private async Task<SessaoEmitida> EntrarAsync(string email, string senha)
    {
        await using var escopo = _sp.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<ServicoSessao>().EntrarAsync(email, senha, Ct);
    }

    private async Task<bool> ValidarAsync(Guid usuarioId, SessaoEmitida s)
    {
        await using var escopo = _sp.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<ValidadorCarimboSeguranca>().ValidarAsync(usuarioId, Carimbo(s), Sessao(s), Ct);
    }

    private async Task<UsuarioAdminDto> ObterAsync(Guid id)
    {
        await using var escopo = _sp.CreateAsyncScope();
        return (await escopo.ServiceProvider.GetRequiredService<IGestaoUsuarios>().ObterAsync(id, Ct))!;
    }

    /// <summary>Chama a porta; com <paramref name="auditar"/>, registra a auditoria antes no mesmo escopo, como os handlers.</summary>
    private async Task NaPortaAsync(Func<IGestaoUsuarios, Task> acao, (string Acao, Guid Id)? auditar = null)
    {
        await using var escopo = _sp.CreateAsyncScope();
        if (auditar is { } a)
            escopo.ServiceProvider.GetRequiredService<IAuditoria>().Registrar(a.Acao, "Usuario", a.Id);
        await acao(escopo.ServiceProvider.GetRequiredService<IGestaoUsuarios>());
    }

    private async Task AssertAuditadoAsync(Guid id, string acao)
    {
        await using var db = sql.CriarContexto();
        Assert.True(await db.EventosAuditoria.AnyAsync(e => e.RecursoId == id && e.Acao == acao, Ct));
    }

    /// <summary>Sessão aberta e validada (o validador grava o carimbo e a sessão no Redis).</summary>
    private async Task<SessaoEmitida> SessaoEmCacheAsync(Guid id, string email, string senha)
    {
        var sessao = await EntrarAsync(email, senha);
        Assert.True(await ValidarAsync(id, sessao));
        return sessao;
    }

    private async Task AssertSessaoRevogadaAsync(Guid id, SessaoEmitida antiga)
    {
        // Sem remover a chave à mão: só passa se a porta limpou o cache do Redis.
        Assert.False(await ValidarAsync(id, antiga));
        await using (var escopo = _sp.CreateAsyncScope())
            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                escopo.ServiceProvider.GetRequiredService<ServicoSessao>().RenovarAsync(antiga.RefreshToken, Ct));
        await using var db = sql.CriarContexto();
        Assert.All(await db.TokensRefresh.Where(t => t.UsuarioId == id).ToListAsync(Ct), t => Assert.NotNull(t.RevogadoEm));
    }

    [Fact]
    public async Task Criar_gera_senha_temporaria_valida_e_exige_troca()
    {
        var (id, email, senha) = await CriarAsync();

        var dto = await ObterAsync(id);
        Assert.Equal((email, Perfis.ProfissionalNap, true, true), (dto.Email, dto.Perfil, dto.Ativo, dto.DeveTrocarSenha));
        var sessao = await EntrarAsync(email, senha);
        Assert.Equal(id, sessao.Usuario.Id);

        // A auditoria registrada antes da porta foi gravada no mesmo SaveChanges.
        await using var db = sql.CriarContexto();
        Assert.True(await db.EventosAuditoria.AnyAsync(e => e.RecursoId == id && e.Acao == "usuario.criar", Ct));
    }

    [Fact]
    public void Senha_temporaria_tem_16_caracteres_sem_ambiguos()
    {
        for (var i = 0; i < 200; i++)
        {
            var senha = GeradorSenhaTemporaria.Gerar();
            Assert.Equal(16, senha.Length);
            Assert.DoesNotContain(senha, c => "0O1lI".Contains(c));
            Assert.Contains(senha, char.IsUpper);
            Assert.Contains(senha, char.IsLower);
            Assert.Contains(senha, char.IsDigit);
        }
    }

    [Fact]
    public async Task Mudar_perfil_rotaciona_carimbo_revoga_sessoes_e_limpa_cache()
    {
        var (id, email, senha) = await CriarAsync();
        var antiga = await SessaoEmCacheAsync(id, email, senha);
        var outra = await SessaoEmCacheAsync(id, email, senha);
        var dto = await ObterAsync(id);

        await NaPortaAsync(p => p.AtualizarAsync(id, dto.Nome, Perfis.Sip, null, dto.Versao, Ct), ("usuario.atualizar", id));

        await AssertSessaoRevogadaAsync(id, antiga);
        await AssertSessaoRevogadaAsync(id, outra);
        await AssertAuditadoAsync(id, "usuario.atualizar");
        var nova = await EntrarAsync(email, senha);
        Assert.NotEqual(Carimbo(antiga), Carimbo(nova));
        Assert.Equal(Perfis.Sip, nova.Usuario.Perfil);
    }

    [Fact]
    public async Task Mudar_so_o_nome_nao_derruba_a_sessao()
    {
        var (id, email, senha) = await CriarAsync();
        var sessao = await SessaoEmCacheAsync(id, email, senha);
        var dto = await ObterAsync(id);

        await NaPortaAsync(p => p.AtualizarAsync(id, "Nome Corrigido", dto.Perfil, dto.CursoId, dto.Versao, Ct));

        Assert.True(await ValidarAsync(id, sessao));
        var depois = await ObterAsync(id);
        Assert.Equal("Nome Corrigido", depois.Nome);
        Assert.NotEqual(dto.Versao, depois.Versao);
    }

    [Fact]
    public async Task Inativar_revoga_sessoes()
    {
        var (id, email, senha) = await CriarAsync();
        var antiga = await SessaoEmCacheAsync(id, email, senha);
        var dto = await ObterAsync(id);

        await NaPortaAsync(p => p.AlterarSituacaoAsync(id, false, dto.Versao, Ct), ("usuario.situacao", id));

        await AssertSessaoRevogadaAsync(id, antiga);
        await AssertAuditadoAsync(id, "usuario.situacao");
        Assert.False((await ObterAsync(id)).Ativo);
        await Assert.ThrowsAsync<UnauthorizedException>(() => EntrarAsync(email, senha));
        Assert.Equal(SqlServerFixture.Relogio().Hoje, await DesativadoEmAsync(id));

        var versao = (await ObterAsync(id)).Versao;
        await NaPortaAsync(p => p.AlterarSituacaoAsync(id, true, versao, Ct), ("usuario.situacao", id));
        Assert.Null(await DesativadoEmAsync(id));
    }

    [Fact]
    public async Task Criar_registra_ativo_desde_com_a_data_de_hoje()
    {
        var (id, _, _) = await CriarAsync();
        await using var db = sql.CriarContexto();
        Assert.Equal(SqlServerFixture.Relogio().Hoje, await db.Users.Where(u => u.Id == id).Select(u => u.AtivoDesde).SingleAsync(Ct));
    }

    private async Task<DateOnly?> DesativadoEmAsync(Guid id)
    {
        await using var db = sql.CriarContexto();
        return await db.Users.Where(u => u.Id == id).Select(u => u.DesativadoEm).SingleAsync(Ct);
    }

    [Fact]
    public async Task Redefinir_senha_revoga_sessoes_e_exige_troca()
    {
        var (id, email, senha) = await CriarAsync();
        await using (var db = sql.CriarContexto())
            await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.DeveTrocarSenha, false), Ct);
        var antiga = await SessaoEmCacheAsync(id, email, senha);
        var nova = GeradorSenhaTemporaria.Gerar();

        var versao = (await ObterAsync(id)).Versao;

        await NaPortaAsync(p => p.RedefinirSenhaAsync(id, nova, versao, Ct), ("usuario.senha.redefinir", id));

        await AssertSessaoRevogadaAsync(id, antiga);
        await AssertAuditadoAsync(id, "usuario.senha.redefinir");
        Assert.True((await ObterAsync(id)).DeveTrocarSenha);
        await Assert.ThrowsAsync<UnauthorizedException>(() => EntrarAsync(email, senha));
        Assert.Equal(id, (await EntrarAsync(email, nova)).Usuario.Id);
    }

    [Fact]
    public async Task Email_duplicado_retorna_conflito()
    {
        var (_, email, _) = await CriarAsync();
        var outroId = Guid.CreateVersion7();

        await using (var escopo = _sp.CreateAsyncScope())
        {
            escopo.ServiceProvider.GetRequiredService<IAuditoria>().Registrar("usuario.criar", "Usuario", outroId);
            var porta = escopo.ServiceProvider.GetRequiredService<IGestaoUsuarios>();
            var ex = await Assert.ThrowsAsync<ConflictException>(() => porta.CriarAsync(
                new NovoUsuario(outroId, email.ToUpperInvariant(), "Outra Pessoa", Perfis.Sip, null), GeradorSenhaTemporaria.Gerar(), Ct));
            Assert.Equal("E-mail já cadastrado.", ex.Message);
        }

        await using var db = sql.CriarContexto();
        Assert.False(await db.Users.AnyAsync(u => u.Id == outroId, Ct));
        Assert.False(await db.EventosAuditoria.AnyAsync(e => e.RecursoId == outroId, Ct));
    }

    [Fact]
    public async Task Versao_antiga_retorna_conflito()
    {
        var (id, email, senha) = await CriarAsync();
        var sessao = await SessaoEmCacheAsync(id, email, senha);
        var v1 = (await ObterAsync(id)).Versao;
        await NaPortaAsync(p => p.AtualizarAsync(id, "Primeira Edição", Perfis.ProfissionalNap, null, v1, Ct));

        await using (var escopo = _sp.CreateAsyncScope())
        {
            escopo.ServiceProvider.GetRequiredService<IAuditoria>().Registrar("usuario.situacao", "Usuario", id);
            var porta = escopo.ServiceProvider.GetRequiredService<IGestaoUsuarios>();
            var ex = await Assert.ThrowsAsync<ConflictException>(() => porta.AlterarSituacaoAsync(id, false, v1, Ct));
            Assert.Equal("O registro foi alterado por outra pessoa. Recarregue e tente novamente", ex.Message);
            await Assert.ThrowsAsync<ConflictException>(() => porta.AtualizarAsync(id, "Segunda Edição", Perfis.Sip, null, v1, Ct));
            // Redefinição com a versão lida antes de uma mudança (ex.: perfil promovido): a alçada conferida não vale mais.
            await Assert.ThrowsAsync<ConflictException>(() => porta.RedefinirSenhaAsync(id, GeradorSenhaTemporaria.Gerar(), v1, Ct));
        }

        var dto = await ObterAsync(id);
        Assert.Equal(("Primeira Edição", Perfis.ProfissionalNap, true), (dto.Nome, dto.Perfil, dto.Ativo));
        Assert.True(await ValidarAsync(id, sessao)); // conflito não revoga nada
        await using var db = sql.CriarContexto();
        Assert.False(await db.EventosAuditoria.AnyAsync(e => e.RecursoId == id && e.Acao == "usuario.situacao", Ct));
    }

    [Fact]
    public async Task Gravacoes_simultaneas_do_mesmo_usuario_a_segunda_falha_por_concorrencia()
    {
        // Cobre a janela entre a leitura (e a conferência do If-Match) e o SaveChanges da porta.
        var (id, _, _) = await CriarAsync();
        await using var a = _sp.CreateAsyncScope();
        await using var b = _sp.CreateAsyncScope();
        var umA = a.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentidade>>();
        var umB = b.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentidade>>();
        var ua = (await umA.FindByIdAsync(id.ToString()))!;
        var ub = (await umB.FindByIdAsync(id.ToString()))!;

        ua.NomeCompleto = "Edição A";
        Assert.True((await umA.UpdateAsync(ua)).Succeeded);
        ub.Ativo = false;
        var resultado = await umB.UpdateSecurityStampAsync(ub);

        Assert.Equal("ConcurrencyFailure", Assert.Single(resultado.Errors).Code);
    }

    [Fact]
    public async Task Lista_filtra_e_conta_administradores_ativos()
    {
        var curso = await sql.CriarCursoAsync();
        var marca = Guid.NewGuid().ToString("N");
        var (id, _, _) = await CriarAsync(Perfis.Administrador, $"{marca}@teste.nap");

        await using var escopo = _sp.CreateAsyncScope();
        var porta = escopo.ServiceProvider.GetRequiredService<IGestaoUsuarios>();
        var pagina = await porta.ListarAsync(new FiltroUsuarios(marca, Perfis.Administrador, null, true, 1, 20), Ct);
        Assert.Equal(id, Assert.Single(pagina.Itens).Id);
        Assert.Equal(1, pagina.Total);
        Assert.Empty((await porta.ListarAsync(new FiltroUsuarios(marca, null, curso, null, 1, 20), Ct)).Itens);
        Assert.True(await porta.ContarAdministradoresAtivosAsync(Ct) >= 1);
    }
}
