using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Auth;
using Nap.Infrastructure.Persistencia;

namespace Nap.Infrastructure.Identidade;

/// <summary>
/// Login, renovação rotativa e logout. Falhas de credencial ou de token lançam <c>UnauthorizedException</c>. Cada login abre uma sessão = uma família de refresh tokens (claim <c>sid</c> do JWT).
/// Atualizações de tokens vão direto ao banco (ExecuteUpdate) para não gravar junto alterações pendentes do contexto.
/// Tokens nunca vão para log.
/// </summary>
public sealed class ServicoSessao(NapDbContext db, UserManager<UsuarioIdentidade> usuarios, ServicoTokens tokens,
    ValidadorCarimboSeguranca carimbos, IRelogio relogio, IAuditoria auditoria) : IServicoSessao
{
    private const string CredenciaisInvalidas = "E-mail ou senha inválidos";

    /// <summary>Janela em que um refresh token já usado ainda renova (duas abas renovando com o mesmo cookie);
    /// fora dela, reapresentar um token usado é reuso e revoga a família inteira.</summary>
    public static readonly TimeSpan ToleranciaReuso = TimeSpan.FromSeconds(30);

    public async Task<SessaoEmitida> EntrarAsync(string email, string senha, CancellationToken ct)
    {
        // Auditoria das falhas: só o id do usuário (o e-mail digitado nunca é gravado), sem usuário autor mesmo que a requisição
        // traga um Bearer; sem o token, um cliente que desiste da requisição não apaga o rastro.
        var usuario = await usuarios.FindByEmailAsync(email ?? "");
        if (usuario is null || await usuarios.IsLockedOutAsync(usuario))
        {
            // Custo equivalente ao da verificação, para o tempo de resposta não revelar se o e-mail existe ou está bloqueado.
            usuarios.PasswordHasher.HashPassword(new UsuarioIdentidade(), senha ?? "");
            await auditoria.RegistrarNegadoAsync(usuario is null ? "login.falha" : "login.bloqueado", "Usuario", usuario?.Id,
                CancellationToken.None, semUsuario: true);
            throw new UnauthorizedException(CredenciaisInvalidas);
        }
        if (!await usuarios.CheckPasswordAsync(usuario, senha ?? ""))
        {
            await usuarios.AccessFailedAsync(usuario);
            await auditoria.RegistrarNegadoAsync("login.falha", "Usuario", usuario.Id, CancellationToken.None, semUsuario: true);
            throw new UnauthorizedException(CredenciaisInvalidas);
        }
        if (!usuario.Ativo)
        {
            await auditoria.RegistrarNegadoAsync("login.falha", "Usuario", usuario.Id, CancellationToken.None, semUsuario: true);
            throw new UnauthorizedException(CredenciaisInvalidas);
        }

        await usuarios.ResetAccessFailedCountAsync(usuario);
        return await EmitirAsync(usuario, Guid.CreateVersion7(), null, ct);
    }

    /// <summary>Rotaciona o refresh token; reapresentar um token revogado, ou usado há mais de <see cref="ToleranciaReuso"/>, revoga toda a família.</summary>
    public async Task<SessaoEmitida> RenovarAsync(string refreshToken, CancellationToken ct)
    {
        var hash = ServicoTokens.Hash(refreshToken ?? "");
        var atual = await db.TokensRefresh.AsNoTracking().SingleOrDefaultAsync(t => t.HashSha256 == hash, ct)
            ?? throw new UnauthorizedException();
        var agora = relogio.Agora;

        // Usado há pouco: outra aba renovando ao mesmo tempo, não reuso malicioso. Segue na mesma família sem
        // reivindicar o antigo (a aba que reivindicou já tem o sucessor).
        var reusoBenigno = atual.UsadoEm is { } usadoEm && agora - usadoEm <= ToleranciaReuso;
        if (atual.RevogadoEm is not null || (atual.UsadoEm is not null && !reusoBenigno))
            throw await RevogarFamiliaAsync(atual.FamiliaId, agora, ct);
        if (atual.ExpiraEm <= agora)
            throw new UnauthorizedException();

        // Carimbo trocado desde a emissão (troca de senha/perfil): a sessão morreu, mesmo que uma
        // renovação concorrente tenha inserido este token depois da revogação.
        // Leitura sem rastreamento: uma entidade já rastreada no escopo poderia trazer carimbo/ativo antigos.
        var usuario = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == atual.UsuarioId, ct);
        if (usuario is not { Ativo: true } || usuario.SecurityStamp != atual.CarimboSeguranca)
            throw await RevogarFamiliaAsync(atual.FamiliaId, agora, ct);

        return await EmitirAsync(usuario, atual.FamiliaId, reusoBenigno ? null : atual.Id, ct);
    }

    /// <summary>Logout: encerra só a sessão do token (sua família de refresh). Token desconhecido não é erro.</summary>
    public async Task EncerrarAsync(string refreshToken, CancellationToken ct)
    {
        var hash = ServicoTokens.Hash(refreshToken ?? "");
        var familiaId = await db.TokensRefresh.Where(t => t.HashSha256 == hash).Select(t => (Guid?)t.FamiliaId).SingleOrDefaultAsync(ct);
        if (familiaId is { } id)
            await RevogarFamiliaAsync(id, relogio.Agora, ct);
    }

    /// <summary>Revoga todas as sessões (famílias de refresh) do usuário e descarta o carimbo e as sessões em cache.
    /// Chamar depois de gravar o novo carimbo; falha do Redis só é logada (a entrada expira em até 30 s).</summary>
    public async Task RevogarTodasAsync(Guid usuarioId, CancellationToken ct)
    {
        var abertas = db.TokensRefresh.Where(t => t.UsuarioId == usuarioId && t.RevogadoEm == null);
        var familias = await abertas.Select(t => t.FamiliaId).Distinct().ToListAsync(ct);
        await abertas.ExecuteUpdateAsync(s => s.SetProperty(t => t.RevogadoEm, relogio.Agora), ct);
        await carimbos.EsquecerUsuarioAsync(usuarioId, ct);
        foreach (var familia in familias)
            await carimbos.EsquecerSessaoAsync(familia, ct);
    }

    /// <summary>
    /// Troca da própria senha: confere a atual, aplica as regras do Identity, zera <c>DeveTrocarSenha</c>, rotaciona o carimbo
    /// e revoga todas as sessões. A sessão que pediu a troca continua numa família nova (mesmo contrato do login).
    /// </summary>
    public async Task<SessaoEmitida> TrocarSenhaAsync(Guid usuarioId, string senhaAtual, string novaSenha, CancellationToken ct)
    {
        var usuario = await usuarios.FindByIdAsync(usuarioId.ToString()) ?? throw new UnauthorizedException();
        // Mesmo bloqueio do login: sem ele, a troca viraria um caminho de força bruta contra a senha atual.
        if (await usuarios.IsLockedOutAsync(usuario))
        {
            await auditoria.RegistrarNegadoAsync("usuario.senha.trocar", "Usuario", usuario.Id, CancellationToken.None);
            throw new ValidationException(new Dictionary<string, string[]> { ["senhaAtual"] = ["Muitas tentativas. Tente novamente mais tarde."] });
        }
        if (!await usuarios.CheckPasswordAsync(usuario, senhaAtual ?? ""))
        {
            await usuarios.AccessFailedAsync(usuario);
            await auditoria.RegistrarNegadoAsync("usuario.senha.trocar", "Usuario", usuario.Id, CancellationToken.None);
            throw new ValidationException(new Dictionary<string, string[]> { ["senhaAtual"] = ["Senha incorreta."] });
        }
        novaSenha ??= "";
        var tamanho = usuarios.Options.Password.RequiredLength;
        if (await usuarios.CheckPasswordAsync(usuario, novaSenha))
            throw new ValidationException(new Dictionary<string, string[]> { ["novaSenha"] = ["A nova senha deve ser diferente da atual."] });
        var erros = new List<string>();
        foreach (var validador in usuarios.PasswordValidators)
            erros.AddRange((await validador.ValidateAsync(usuarios, usuario, novaSenha)).Errors.Select(e => GestaoUsuarios.Traduzir(e, tamanho)));
        if (erros.Count > 0)
            throw new ValidationException(new Dictionary<string, string[]> { ["novaSenha"] = [.. erros] });

        auditoria.Registrar("usuario.senha.trocar", "Usuario", usuario.Id);
        // Hash direto, como na redefinição: um único SaveChanges grava senha, flag, carimbo novo e auditoria.
        usuario.PasswordHash = usuarios.PasswordHasher.HashPassword(usuario, novaSenha);
        usuario.DeveTrocarSenha = false;
        usuario.AccessFailedCount = 0;
        if (!(await usuarios.UpdateSecurityStampAsync(usuario)).Succeeded)
        {
            db.ChangeTracker.Clear(); // a auditoria não pode ser gravada por um SaveChanges posterior
            throw new ConflictException(UnidadeDeTrabalho.MensagemRegistroAlterado);
        }
        // Revoga antes de emitir (senão a família nova cairia junto); sem o token da requisição: o carimbo já foi gravado.
        await RevogarTodasAsync(usuario.Id, CancellationToken.None);
        return await EmitirAsync(usuario, Guid.CreateVersion7(), null, ct);
    }

    private async Task<SessaoEmitida> EmitirAsync(UsuarioIdentidade usuario, Guid familiaId, Guid? anteriorId, CancellationToken ct)
    {
        var agora = relogio.Agora;
        var refresh = ServicoTokens.NovoRefresh();
        var novo = new TokenRefresh(Guid.CreateVersion7(), usuario.Id, familiaId, ServicoTokens.Hash(refresh),
            agora + ServicoTokens.DuracaoRefresh, usuario.SecurityStamp ?? "");

        await using var transacao = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (anteriorId is { } id)
            // Reivindicação atômica: de duas renovações simultâneas com o mesmo token (duas abas), só uma marca UsadoEm;
            // a outra perdeu por milissegundos e segue na mesma família sem reivindicar (mesma regra de ToleranciaReuso).
            await db.TokensRefresh
                .Where(t => t.Id == id && t.UsadoEm == null && t.RevogadoEm == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsadoEm, agora).SetProperty(t => t.SubstituidoPorId, novo.Id), ct);
        // Família revogada entre a leitura e aqui (logout ou reuso concorrente): um token novo a ressuscitaria. Famílias
        // são revogadas inteiras, então qualquer token revogado basta; no login a família é nova e nada é encontrado.
        if (await db.TokensRefresh.AnyAsync(t => t.FamiliaId == familiaId && t.RevogadoEm != null, ct))
        {
            await transacao.RollbackAsync(CancellationToken.None);
            throw new UnauthorizedException();
        }
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO [nap].[TokensRefresh] (Id, UsuarioId, FamiliaId, HashSha256, ExpiraEm, CarimboSeguranca) VALUES ({novo.Id}, {novo.UsuarioId}, {novo.FamiliaId}, {novo.HashSha256}, {novo.ExpiraEm.ToUniversalTime()}, {novo.CarimboSeguranca})",
            ct);
        await transacao.CommitAsync(ct);

        var (access, expiraEm) = tokens.EmitirAccess(usuario, familiaId, agora);
        return new SessaoEmitida(access, expiraEm, refresh, novo.ExpiraEm, await MontarUsuarioAsync(usuario, ct));
    }

    /// <summary>Dados do usuário da sessão atual (<c>GET /auth/eu</c>). Usuário inexistente → <c>UnauthorizedException</c>.</summary>
    public async Task<UsuarioSessaoDto> ObterUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw new UnauthorizedException();
        return await MontarUsuarioAsync(usuario, ct);
    }

    private async Task<UsuarioSessaoDto> MontarUsuarioAsync(UsuarioIdentidade usuario, CancellationToken ct)
    {
        var curso = usuario.CursoId is { } cursoId
            ? await db.Cursos.AsNoTracking().Where(c => c.Id == cursoId).Select(c => c.Nome).SingleOrDefaultAsync(ct)
            : null;
        return new UsuarioSessaoDto(usuario.Id, usuario.NomeCompleto, usuario.Email ?? "", usuario.Perfil, usuario.CursoId, curso,
            usuario.DeveTrocarSenha);
    }

    private async Task<UnauthorizedException> RevogarFamiliaAsync(Guid familiaId, DateTimeOffset agora, CancellationToken ct)
    {
        await db.TokensRefresh.Where(t => t.FamiliaId == familiaId && t.RevogadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevogadoEm, agora), ct);
        await carimbos.EsquecerSessaoAsync(familiaId, ct);
        return new UnauthorizedException();
    }
}
