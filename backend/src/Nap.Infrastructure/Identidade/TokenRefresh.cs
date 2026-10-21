namespace Nap.Infrastructure.Identidade;

/// <summary>Refresh token rotativo. Só o hash SHA-256 é persistido; tokens da mesma cadeia de renovações
/// compartilham a <see cref="FamiliaId"/>, revogada inteira quando um token já usado é reapresentado.</summary>
public class TokenRefresh
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid FamiliaId { get; private set; }
    public string HashSha256 { get; private set; } = null!;
    public DateTimeOffset ExpiraEm { get; private set; }
    public DateTimeOffset? UsadoEm { get; private set; }
    public DateTimeOffset? RevogadoEm { get; private set; }
    public Guid? SubstituidoPorId { get; private set; }
    /// <summary>SecurityStamp do usuário na emissão; a renovação exige que ainda seja o atual.</summary>
    public string CarimboSeguranca { get; private set; } = null!;

    private TokenRefresh() { } // ORM

    public TokenRefresh(Guid id, Guid usuarioId, Guid familiaId, string hashSha256, DateTimeOffset expiraEm, string carimboSeguranca)
    {
        Id = id;
        UsuarioId = usuarioId;
        FamiliaId = familiaId;
        HashSha256 = hashSha256;
        ExpiraEm = expiraEm;
        CarimboSeguranca = carimboSeguranca;
    }
}
