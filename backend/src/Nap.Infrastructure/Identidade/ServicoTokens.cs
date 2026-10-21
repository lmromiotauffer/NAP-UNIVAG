using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Nap.Application.Features.Auth;

namespace Nap.Infrastructure.Identidade;

/// <summary>
/// Emite o access token (JWT HMAC-SHA256, 15 min) e gera refresh tokens opacos.
/// Os nomes de claim abaixo são os nomes EXATOS do token e também os que <see cref="UsuarioAtual"/> lê. Por isso a
/// validação na Api deve usar <c>MapInboundClaims = false</c> (senão "sub"/"role" viram URIs do ClaimTypes),
/// <c>NameClaimType = "name"</c> e <c>RoleClaimType = "role"</c> (para <c>[Authorize(Roles = ...)]</c> enxergar o perfil).
/// </summary>
public sealed class ServicoTokens(IOptions<OpcoesJwt> opcoes)
{
    public const string ClaimId = JwtRegisteredClaimNames.Sub;
    public const string ClaimNome = JwtRegisteredClaimNames.Name;
    public const string ClaimPerfil = "role";
    public const string ClaimCurso = "curso";
    public const string ClaimCarimbo = "stamp";
    /// <summary>Sessão = família de refresh tokens; o logout encerra só ela.</summary>
    public const string ClaimSessao = JwtRegisteredClaimNames.Sid;

    public static readonly TimeSpan DuracaoAccess = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DuracaoRefresh = TimeSpan.FromDays(7);

    public (string Token, DateTimeOffset ExpiraEm) EmitirAccess(UsuarioIdentidade usuario, Guid familiaId, DateTimeOffset agora)
    {
        var o = opcoes.Value;
        var expiraEm = agora + DuracaoAccess;
        var claims = new Dictionary<string, object>
        {
            [ClaimId] = usuario.Id.ToString(),
            [ClaimNome] = usuario.NomeCompleto,
            [ClaimPerfil] = usuario.Perfil,
            [ClaimCarimbo] = usuario.SecurityStamp ?? throw new InvalidOperationException("Usuário sem carimbo de segurança."),
            [ClaimSessao] = familiaId.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
        };
        if (usuario.CursoId is { } curso)
            claims[ClaimCurso] = curso.ToString();
        if (usuario.DeveTrocarSenha)
            claims[ClaimsNap.TrocarSenha] = "1";

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.Emissor,
            Audience = o.Audiencia,
            Claims = claims,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            SigningCredentials = new SigningCredentials(o.ChaveDeAssinatura(), SecurityAlgorithms.HmacSha256),
        });
        return (token, expiraEm);
    }

    /// <summary>32 bytes aleatórios em base64url. Só o chamador vê o valor; o banco guarda <see cref="Hash"/>.</summary>
    public static string NovoRefresh() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string refreshToken) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
