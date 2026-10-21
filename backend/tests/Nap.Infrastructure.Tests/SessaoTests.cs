using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Nap.Application.Comum;
using Nap.Application.Features.Auth;
using Nap.Domain.Comum;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Tests;

public class SessaoTests(SqlServerFixture sql, RedisFixture redis) : IAsyncDisposable
{
    private const string Senha = "SenhaForte2026";
    private const string MensagemLogin = "E-mail ou senha inválidos";
    private readonly ServiceProvider _sp = RedisFixture.CriarProvedor(sql, redis);

    public ValueTask DisposeAsync() => _sp.DisposeAsync();

    private async Task<UsuarioIdentidade> CriarUsuarioAsync(bool ativo = true, Guid? cursoId = null)
    {
        await using var escopo = _sp.CreateAsyncScope();
        var id = Guid.CreateVersion7();
        var usuario = new UsuarioIdentidade
        {
            Id = id, UserName = $"{id:N}@teste.nap", Email = $"{id:N}@teste.nap", NomeCompleto = "Usuária da sessão",
            Perfil = cursoId is null ? Perfis.ProfissionalNap : Perfis.CoordenadorCurso, CursoId = cursoId, Ativo = ativo,
        };
        var resultado = await escopo.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentidade>>().CreateAsync(usuario, Senha);
        Assert.True(resultado.Succeeded, string.Join("; ", resultado.Errors.Select(e => e.Description)));
        return usuario;
    }

    private static string Carimbo(SessaoEmitida s) => new JsonWebTokenHandler().ReadJsonWebToken(s.AccessToken).GetClaim("stamp").Value;

    private static Guid Sessao(SessaoEmitida s) => Guid.Parse(new JsonWebTokenHandler().ReadJsonWebToken(s.AccessToken).GetClaim("sid").Value);

    [Fact]
    public async Task Login_com_senha_errada_e_usuario_inativo_tem_mesma_resposta()
    {
        var ct = TestContext.Current.CancellationToken;
        var ativo = await CriarUsuarioAsync();
        var inativo = await CriarUsuarioAsync(ativo: false);
        await using var escopo = _sp.CreateAsyncScope();
        var sessao = escopo.ServiceProvider.GetRequiredService<ServicoSessao>();

        var erros = new[]
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.EntrarAsync(ativo.Email!, "SenhaErrada2026", ct)),
            await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.EntrarAsync(inativo.Email!, Senha, ct)),
            await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.EntrarAsync("ninguem@teste.nap", Senha, ct)),
        };
        Assert.All(erros, e => Assert.Equal(MensagemLogin, e.Message));

        var emitida = await sessao.EntrarAsync(ativo.Email!, Senha, ct);
        Assert.Equal((ativo.Id, Perfis.ProfissionalNap), (emitida.Usuario.Id, emitida.Usuario.Perfil));
        Assert.InRange(emitida.ExpiraEm - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
        Assert.InRange(emitida.RefreshExpiraEm - DateTimeOffset.UtcNow, TimeSpan.FromDays(6.9), TimeSpan.FromDays(7));

        // Lockout do Identity: após 5 tentativas erradas nem a senha certa entra, e a resposta é a mesma.
        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.EntrarAsync(ativo.Email!, "SenhaErrada2026", ct));
        Assert.Equal(MensagemLogin, (await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.EntrarAsync(ativo.Email!, Senha, ct))).Message);
    }

    [Fact]
    public async Task Refresh_rotaciona_e_token_antigo_revoga_a_familia()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CriarUsuarioAsync();
        await using var escopo = _sp.CreateAsyncScope();
        var sessao = escopo.ServiceProvider.GetRequiredService<ServicoSessao>();

        var t1 = await sessao.EntrarAsync(usuario.Email!, Senha, ct);
        var t2 = await sessao.RenovarAsync(t1.RefreshToken, ct);
        Assert.NotEqual(t1.RefreshToken, t2.RefreshToken);

        // Passada a tolerância de reuso (outra aba), reapresentar t1 é reuso de verdade.
        await EnvelhecerUsoAsync(t1.RefreshToken, ct);
        await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.RenovarAsync(t1.RefreshToken, ct));
        await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.RenovarAsync(t2.RefreshToken, ct));
        await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.RenovarAsync("token-inexistente", ct));

        await using var db = sql.CriarContexto();
        var tokens = await db.TokensRefresh.Where(t => t.UsuarioId == usuario.Id).ToListAsync(ct);
        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens.Select(t => t.FamiliaId).Distinct());
        Assert.All(tokens, t => Assert.NotNull(t.RevogadoEm));
        Assert.DoesNotContain(tokens, t => t.HashSha256 == t1.RefreshToken || t.HashSha256 == t2.RefreshToken);
        Assert.Contains(tokens, t => t.SubstituidoPorId is not null && t.UsadoEm is not null);
    }

    [Fact]
    public async Task Logout_encerra_so_a_sessao_apresentada()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CriarUsuarioAsync();
        await using var escopo = _sp.CreateAsyncScope();
        var sessao = escopo.ServiceProvider.GetRequiredService<ServicoSessao>();
        var validador = escopo.ServiceProvider.GetRequiredService<ValidadorCarimboSeguranca>();

        var a = await sessao.EntrarAsync(usuario.Email!, Senha, ct);
        var b = await sessao.EntrarAsync(usuario.Email!, Senha, ct);
        Assert.True(await validador.ValidarAsync(usuario.Id, Carimbo(a), Sessao(a), ct)); // agora em cache
        await sessao.EncerrarAsync(a.RefreshToken, ct);
        await sessao.EncerrarAsync("token-inexistente", ct);

        await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.RenovarAsync(a.RefreshToken, ct));
        var b2 = await sessao.RenovarAsync(b.RefreshToken, ct);
        Assert.Equal(Carimbo(a), Carimbo(b2)); // logout não troca o carimbo
        Assert.Equal(Sessao(b), Sessao(b2));
        Assert.False(await validador.ValidarAsync(usuario.Id, Carimbo(a), Sessao(a), ct));
        Assert.True(await validador.ValidarAsync(usuario.Id, Carimbo(b2), Sessao(b2), ct));

        await using var db = sql.CriarContexto();
        var semRedis = new ValidadorCarimboSeguranca(db, new CacheQueLanca(), SqlServerFixture.Relogio(), NullLogger<ValidadorCarimboSeguranca>.Instance);
        Assert.False(await semRedis.ValidarAsync(usuario.Id, Carimbo(a), Sessao(a), ct));
        Assert.True(await semRedis.ValidarAsync(usuario.Id, Carimbo(b2), Sessao(b2), ct));
        Assert.False(await semRedis.ValidarAsync(Guid.NewGuid(), Carimbo(b2), Sessao(b2), ct)); // sid de outro usuário
    }

    /// <summary>Marca o token como usado antes da tolerância: reapresentá-lo passa a ser reuso.</summary>
    private async Task EnvelhecerUsoAsync(string refreshToken, CancellationToken ct)
    {
        await using var db = sql.CriarContexto();
        var hash = ServicoTokens.Hash(refreshToken);
        var usadoEm = DateTimeOffset.UtcNow - ServicoSessao.ToleranciaReuso - TimeSpan.FromSeconds(1);
        await db.TokensRefresh.Where(t => t.HashSha256 == hash).ExecuteUpdateAsync(s => s.SetProperty(t => t.UsadoEm, usadoEm), ct);
    }

    [Fact]
    public async Task Renovacoes_concorrentes_do_mesmo_token_passam_todas_e_mantem_a_familia()
    {
        // Duas (ou mais) abas restauradas de uma vez renovam com o mesmo cookie: ninguém pode ser deslogado.
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CriarUsuarioAsync();
        SessaoEmitida t1;
        await using (var escopo = _sp.CreateAsyncScope())
            t1 = await escopo.ServiceProvider.GetRequiredService<ServicoSessao>().EntrarAsync(usuario.Email!, Senha, ct);

        var tentativas = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await using var escopo = _sp.CreateAsyncScope();
            return await escopo.ServiceProvider.GetRequiredService<ServicoSessao>().RenovarAsync(t1.RefreshToken, ct);
        }, ct)));

        Assert.All(tentativas, t => Assert.Equal(Sessao(t1), Sessao(t)));
        await using var db = sql.CriarContexto();
        var familia = await db.TokensRefresh.Where(t => t.FamiliaId == Sessao(t1)).ToListAsync(ct);
        Assert.Equal(9, familia.Count);
        Assert.All(familia, t => Assert.Null(t.RevogadoEm));
        Assert.Single(familia, t => t.SubstituidoPorId is not null); // só uma renovação reivindicou o original
    }

    [Fact]
    public async Task Reuso_dentro_da_tolerancia_renova_e_fora_dela_revoga_a_familia()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CriarUsuarioAsync();
        await using var escopo = _sp.CreateAsyncScope();
        var sessao = escopo.ServiceProvider.GetRequiredService<ServicoSessao>();

        var t1 = await sessao.EntrarAsync(usuario.Email!, Senha, ct);
        var t2 = await sessao.RenovarAsync(t1.RefreshToken, ct);
        var t3 = await sessao.RenovarAsync(t1.RefreshToken, ct); // segunda aba, logo depois
        Assert.Equal(Sessao(t1), Sessao(t3));

        await EnvelhecerUsoAsync(t1.RefreshToken, ct);
        await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.RenovarAsync(t1.RefreshToken, ct));
        await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.RenovarAsync(t2.RefreshToken, ct));
        await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.RenovarAsync(t3.RefreshToken, ct));
    }

    [Fact]
    public async Task Renovacao_falha_quando_o_carimbo_mudou()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CriarUsuarioAsync();
        await using var escopo = _sp.CreateAsyncScope();
        var sessao = escopo.ServiceProvider.GetRequiredService<ServicoSessao>();
        var emitida = await sessao.EntrarAsync(usuario.Email!, Senha, ct);

        // Troca de carimbo sem revogar (ex.: troca de senha).
        await using (var outro = _sp.CreateAsyncScope())
        {
            var um = outro.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentidade>>();
            Assert.True((await um.UpdateSecurityStampAsync((await um.FindByIdAsync(usuario.Id.ToString()))!)).Succeeded);
        }

        await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.RenovarAsync(emitida.RefreshToken, ct));
        await using var db = sql.CriarContexto();
        Assert.All(await db.TokensRefresh.Where(t => t.FamiliaId == Sessao(emitida)).ToListAsync(ct), t => Assert.NotNull(t.RevogadoEm));
    }

    [Fact]
    public async Task Stamp_alterado_invalida_validacao()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CriarUsuarioAsync();
        await using var escopo = _sp.CreateAsyncScope();
        var sessao = escopo.ServiceProvider.GetRequiredService<ServicoSessao>();
        var validador = escopo.ServiceProvider.GetRequiredService<ValidadorCarimboSeguranca>();

        var antiga = await sessao.EntrarAsync(usuario.Email!, Senha, ct);
        Assert.True(await validador.ValidarAsync(usuario.Id, Carimbo(antiga), Sessao(antiga), ct)); // agora em cache
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentidade>>();
        Assert.True((await usuarios.UpdateSecurityStampAsync((await usuarios.FindByIdAsync(usuario.Id.ToString()))!)).Succeeded);
        await escopo.ServiceProvider.GetRequiredService<IDistributedCache>().RemoveAsync(ValidadorCarimboSeguranca.Chave(usuario.Id), ct);

        Assert.False(await validador.ValidarAsync(usuario.Id, Carimbo(antiga), Sessao(antiga), ct));
        var nova = await sessao.EntrarAsync(usuario.Email!, Senha, ct);
        Assert.NotEqual(Carimbo(antiga), Carimbo(nova));
        Assert.True(await validador.ValidarAsync(usuario.Id, Carimbo(nova), Sessao(nova), ct));
    }

    [Fact]
    public async Task Redis_indisponivel_valida_stamp_pelo_banco()
    {
        var ct = TestContext.Current.CancellationToken;
        var ativo = await CriarUsuarioAsync();
        var inativo = await CriarUsuarioAsync(ativo: false);
        SessaoEmitida emitida;
        await using (var escopo = _sp.CreateAsyncScope())
            emitida = await escopo.ServiceProvider.GetRequiredService<ServicoSessao>().EntrarAsync(ativo.Email!, Senha, ct);
        await using var db = sql.CriarContexto();
        var validador = new ValidadorCarimboSeguranca(db, new CacheQueLanca(), SqlServerFixture.Relogio(), NullLogger<ValidadorCarimboSeguranca>.Instance);

        Assert.True(await validador.ValidarAsync(ativo.Id, Carimbo(emitida), Sessao(emitida), ct));
        Assert.False(await validador.ValidarAsync(ativo.Id, "carimbo-antigo", Sessao(emitida), ct));
        Assert.False(await validador.ValidarAsync(ativo.Id, Carimbo(emitida), Guid.NewGuid(), ct));
        Assert.False(await validador.ValidarAsync(inativo.Id, inativo.SecurityStamp!, Sessao(emitida), ct));
        Assert.False(await validador.ValidarAsync(Guid.NewGuid(), Carimbo(emitida), Sessao(emitida), ct));
    }

    [Fact]
    public async Task Usuario_inativo_nao_passa_na_validacao_mesmo_com_cache_antigo_expirado()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CriarUsuarioAsync();
        await using var escopo = _sp.CreateAsyncScope();
        var validador = escopo.ServiceProvider.GetRequiredService<ValidadorCarimboSeguranca>();
        var emitida = await escopo.ServiceProvider.GetRequiredService<ServicoSessao>().EntrarAsync(usuario.Email!, Senha, ct);
        Assert.True(await validador.ValidarAsync(usuario.Id, Carimbo(emitida), Sessao(emitida), ct));

        await using (var db = sql.CriarContexto())
            await db.Users.Where(u => u.Id == usuario.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Ativo, false), ct);
        // Simula o fim dos 30 s de TTL da entrada antiga ("stamp|True").
        await escopo.ServiceProvider.GetRequiredService<IDistributedCache>().RemoveAsync(ValidadorCarimboSeguranca.Chave(usuario.Id), ct);

        Assert.False(await validador.ValidarAsync(usuario.Id, Carimbo(emitida), Sessao(emitida), ct));
    }

    [Fact]
    public async Task Token_emitido_e_lido_por_UsuarioAtual_com_os_mesmos_nomes_de_claim()
    {
        var ct = TestContext.Current.CancellationToken;
        var curso = await sql.CriarCursoAsync("Curso da sessão");
        var usuario = await CriarUsuarioAsync(cursoId: curso);
        await using var escopo = _sp.CreateAsyncScope();
        var emitida = await escopo.ServiceProvider.GetRequiredService<ServicoSessao>().EntrarAsync(usuario.Email!, Senha, ct);
        var jwt = escopo.ServiceProvider.GetRequiredService<IOptions<OpcoesJwt>>().Value;

        var validacao = await new JsonWebTokenHandler().ValidateTokenAsync(emitida.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = jwt.Emissor, ValidAudience = jwt.Audiencia, IssuerSigningKey = jwt.ChaveDeAssinatura(),
        });
        Assert.True(validacao.IsValid, validacao.Exception?.Message);
        var acessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(validacao.ClaimsIdentity) } };
        var atual = new UsuarioAtual(acessor);

        Assert.Equal((usuario.Id, Perfis.CoordenadorCurso, curso), (atual.Id, atual.Perfil, atual.CursoId));
        Assert.Equal("Curso da sessão", emitida.Usuario.Curso);
        acessor.HttpContext = null; // o acessor guarda o contexto em AsyncLocal: zera-se o mesmo, não um novo
        Assert.Throws<UnauthorizedException>(() => atual.Id);
    }

    [Fact]
    public async Task Chave_jwt_curta_e_recusada()
    {
        await using var sp = RedisFixture.CriarProvedor(sql, redis, ajustes: new Dictionary<string, string?> { ["Jwt:Chave"] = "curta" });
        Assert.Throws<OptionsValidationException>(() => sp.GetRequiredService<IOptions<OpcoesJwt>>().Value);
    }

    private sealed class CacheQueLanca : IDistributedCache
    {
        private static InvalidOperationException Falha() => new("Redis indisponível (simulado)");
        public byte[]? Get(string key) => throw Falha();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Falha();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Falha();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Falha();
        public void Refresh(string key) => throw Falha();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Falha();
        public void Remove(string key) => throw Falha();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Falha();
    }
}
