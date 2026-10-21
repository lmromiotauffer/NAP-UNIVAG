using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Nap.Infrastructure.Identidade;

/// <summary>Seção <c>Jwt</c> da configuração. A chave nunca fica no código: vem de configuração/segredo do ambiente.</summary>
public sealed class OpcoesJwt
{
    /// <summary>Chave HMAC-SHA256; no mínimo 32 bytes em UTF-8 (validado na inicialização).</summary>
    public string Chave { get; set; } = "";
    public string Emissor { get; set; } = "nap";
    public string Audiencia { get; set; } = "nap";

    public SymmetricSecurityKey ChaveDeAssinatura() => new(Encoding.UTF8.GetBytes(Chave));

    internal static bool ChaveValida(OpcoesJwt o) => Encoding.UTF8.GetByteCount(o.Chave ?? "") >= 32;
}
