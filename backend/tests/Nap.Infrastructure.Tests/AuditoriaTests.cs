using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nap.Application.Comum;
using Nap.Application.Features.Auditoria;
using Nap.Domain.Comum;
using Nap.Infrastructure.Identidade;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Tests;

public class AuditoriaTests(SqlServerFixture sql, RedisFixture redis) : IAsyncDisposable
{
    private const string Senha = "SenhaForte2026";
    private readonly ServiceProvider _sp = RedisFixture.CriarProvedor(sql, redis);

    public ValueTask DisposeAsync() => _sp.DisposeAsync();

    // Pelo UserManager (não por db.Users.Add): só assim o lockout fica habilitado.
    private async Task<UsuarioIdentidade> CriarUsuarioAsync()
    {
        await using var escopo = _sp.CreateAsyncScope();
        var id = Guid.CreateVersion7();
        var usuario = new UsuarioIdentidade
        {
            Id = id, UserName = $"{id:N}@teste.nap", Email = $"{id:N}@teste.nap", NomeCompleto = "Usuária da auditoria",
            Perfil = Perfis.ProfissionalNap, Ativo = true,
        };
        var resultado = await escopo.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentidade>>().CreateAsync(usuario, Senha);
        Assert.True(resultado.Succeeded, string.Join("; ", resultado.Errors.Select(e => e.Description)));
        return usuario;
    }

    // Sessão autenticada na requisição (como um Bearer válido), lida por IUsuarioAtual.
    private void Autenticar(Guid usuarioId) =>
        _sp.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ServicoTokens.ClaimId, usuarioId.ToString())], "Teste")),
        };

    private async Task<List<EventoAuditoria>> EventosAsync(Func<EventoAuditoria, bool> filtro)
    {
        await using var db = sql.CriarContexto();
        return [.. (await db.EventosAuditoria.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken)).Where(filtro)];
    }

    [Fact]
    public async Task Negado_e_gravado_mesmo_com_rollback()
    {
        var ct = TestContext.Current.CancellationToken;
        var recursoId = Guid.NewGuid();
        await using (var escopo = _sp.CreateAsyncScope())
        {
            var auditoria = escopo.ServiceProvider.GetRequiredService<IAuditoria>();
            var uow = escopo.ServiceProvider.GetRequiredService<IUnidadeDeTrabalho>();
            await Assert.ThrowsAsync<ForbiddenException>(() => uow.EmTransacaoAsync<int>(async c =>
            {
                auditoria.Registrar("teste.sucesso", "Teste", recursoId);
                await uow.SalvarAsync(c); // gravado dentro da transação que será desfeita
                await auditoria.RegistrarNegadoAsync("acesso.negado", "GET api/teste/{id}", recursoId, CancellationToken.None);
                throw new ForbiddenException();
            }, ct));
        }

        var eventos = await EventosAsync(e => e.RecursoId == recursoId);
        var negado = Assert.Single(eventos); // o "sucesso" saiu com o rollback; o negado ficou
        Assert.Equal(("acesso.negado", "GET api/teste/{id}", "negado"), (negado.Acao, negado.Recurso, negado.Resultado));
    }

    [Fact]
    public async Task Login_com_email_inexistente_grava_falha_sem_usuario()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = $"{Guid.NewGuid():N}@inexistente.nap";
        var antes = DateTimeOffset.UtcNow.AddSeconds(-1);
        await using var escopo = _sp.CreateAsyncScope();

        await Assert.ThrowsAsync<UnauthorizedException>(() => escopo.ServiceProvider.GetRequiredService<ServicoSessao>().EntrarAsync(email, Senha, ct));

        var eventos = await EventosAsync(e => e.Acao == "login.falha" && e.Em >= antes && e.RecursoId == null);
        Assert.NotEmpty(eventos);
        Assert.All(eventos, e => Assert.Equal(("Usuario", "negado", (Guid?)null), (e.Recurso, e.Resultado, e.UsuarioId)));
        await using var db = sql.CriarContexto();
        Assert.False(await db.EventosAuditoria.AnyAsync(e => e.Acao.Contains(email) || e.Recurso.Contains(email), ct)); // o e-mail nunca é gravado
    }

    [Fact]
    public async Task Login_de_conta_bloqueada_grava_bloqueio()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CriarUsuarioAsync();
        await using var escopo = _sp.CreateAsyncScope();
        var sessao = escopo.ServiceProvider.GetRequiredService<ServicoSessao>();

        for (var i = 0; i < 5; i++) // a quinta falha bloqueia a conta e ainda é login.falha
            await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.EntrarAsync(usuario.Email!, "SenhaErrada2026", ct));
        await Assert.ThrowsAsync<UnauthorizedException>(() => sessao.EntrarAsync(usuario.Email!, Senha, ct));

        var eventos = await EventosAsync(e => e.RecursoId == usuario.Id);
        Assert.Equal(5, eventos.Count(e => e.Acao == "login.falha"));
        var bloqueio = Assert.Single(eventos, e => e.Acao == "login.bloqueado");
        Assert.Equal(("Usuario", "negado", (Guid?)null), (bloqueio.Recurso, bloqueio.Resultado, bloqueio.UsuarioId));
    }

    [Fact]
    public async Task Troca_de_senha_com_senha_errada_grava_negado()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CriarUsuarioAsync();
        Autenticar(usuario.Id);
        await using var escopo = _sp.CreateAsyncScope();

        await Assert.ThrowsAsync<ValidationException>(() => escopo.ServiceProvider.GetRequiredService<ServicoSessao>()
            .TrocarSenhaAsync(usuario.Id, "SenhaErrada2026", "OutraSenha2026", ct));

        var evento = Assert.Single(await EventosAsync(e => e.RecursoId == usuario.Id));
        Assert.Equal(("usuario.senha.trocar", "Usuario", "negado", (Guid?)usuario.Id),
            (evento.Acao, evento.Recurso, evento.Resultado, evento.UsuarioId));
    }

    [Fact]
    public async Task Login_com_sessao_autenticada_grava_sem_usuario()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CriarUsuarioAsync();
        var outro = await CriarUsuarioAsync();
        Autenticar(outro.Id); // Bearer de outra sessão junto do login
        await using var escopo = _sp.CreateAsyncScope();

        await Assert.ThrowsAsync<UnauthorizedException>(() => escopo.ServiceProvider.GetRequiredService<ServicoSessao>()
            .EntrarAsync(usuario.Email!, "SenhaErrada2026", ct));

        var evento = Assert.Single(await EventosAsync(e => e.RecursoId == usuario.Id));
        Assert.Equal(("login.falha", (Guid?)null), (evento.Acao, evento.UsuarioId));
    }

    private async Task GravarAsync(params EventoAuditoria[] eventos)
    {
        await using var db = sql.CriarContexto();
        db.EventosAuditoria.AddRange(eventos);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private AuditoriaQueries Consultas(NapDbContext db) => new(db, SqlServerFixture.Relogio());

    private static FiltroAuditoria Filtro(Guid usuarioId, string? acao = null, string? resultado = null, DateOnly? inicio = null,
        DateOnly? fim = null)
    {
        var hoje = SqlServerFixture.Relogio().Hoje;
        return new(inicio ?? hoje.AddDays(-1), fim ?? hoje.AddDays(1), usuarioId, acao, null, null, resultado, 1, 100);
    }

    [Fact]
    public async Task Lista_filtra_por_prefixo_de_acao_e_resultado()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuarioId = await sql.CriarProfissionalAsync(nome: "Quem audita");
        var em = DateTimeOffset.UtcNow;
        await GravarAsync(
            new EventoAuditoria(usuarioId, "pei.criar", "Pei", null, "sucesso", em),
            new EventoAuditoria(usuarioId, "pei.publicar", "VersaoPei", null, "negado", em.AddSeconds(1)),
            new EventoAuditoria(usuarioId, "peis.x", "Pei", null, "sucesso", em.AddSeconds(2)),
            new EventoAuditoria(usuarioId, "aluno.criar", "Aluno", null, "sucesso", em.AddSeconds(3)));
        await using var db = sql.CriarContexto();

        var pei = await Consultas(db).ListarAsync(Filtro(usuarioId, acao: "pei."), ct);
        Assert.Equal(["pei.publicar", "pei.criar"], pei.Itens.Select(e => e.Acao)); // mais recente primeiro; "peis.x" fora
        Assert.Equal(2, pei.Total);
        Assert.All(pei.Itens, e => Assert.Equal("Quem audita", e.Usuario));

        var negados = await Consultas(db).ListarAsync(Filtro(usuarioId, acao: "pei.", resultado: "negado"), ct);
        Assert.Equal("pei.publicar", Assert.Single(negados.Itens).Acao);
    }

    [Fact]
    public async Task Fim_inclui_o_dia_inteiro_no_fuso()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuarioId = Guid.NewGuid(); // sem usuário cadastrado: Usuario nulo
        var cuiaba = TimeSpan.FromHours(-4);
        await GravarAsync(
            new EventoAuditoria(usuarioId, "t.antes", "T", null, "sucesso", new(2026, 9, 9, 23, 59, 0, cuiaba)),
            new EventoAuditoria(usuarioId, "t.inicio", "T", null, "sucesso", new(2026, 9, 10, 0, 0, 0, cuiaba)),
            new EventoAuditoria(usuarioId, "t.fim", "T", null, "sucesso", new(2026, 9, 12, 23, 59, 0, cuiaba)), // 03:59Z do dia 13
            new EventoAuditoria(usuarioId, "t.depois", "T", null, "sucesso", new(2026, 9, 13, 0, 0, 0, cuiaba)));
        await using var db = sql.CriarContexto();

        var lista = await Consultas(db).ListarAsync(Filtro(usuarioId, inicio: new(2026, 9, 10), fim: new(2026, 9, 12)), ct);

        Assert.Equal(["t.fim", "t.inicio"], lista.Itens.Select(e => e.Acao));
        Assert.All(lista.Itens, e => Assert.Null(e.Usuario));
    }
}
