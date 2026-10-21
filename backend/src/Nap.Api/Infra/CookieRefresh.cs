namespace Nap.Api.Infra;

/// <summary>Cookie do refresh token: nunca vai no corpo nem em log. CSRF: SameSite=Strict + header <c>X-Requested-With</c>.</summary>
public static class CookieRefresh
{
    public const string Nome = "nap_refresh";
    private const string Caminho = "/api/auth";

    public static void Gravar(HttpResponse resposta, string token, DateTimeOffset expiraEm) =>
        resposta.Cookies.Append(Nome, token, Opcoes(expiraEm));

    public static string? Ler(HttpRequest requisicao) => requisicao.Cookies[Nome];

    // Mesmo Path/atributos da gravação, senão o navegador ignora a remoção.
    public static void Remover(HttpResponse resposta) => resposta.Cookies.Delete(Nome, Opcoes(null));

    public static bool CabecalhoSpaPresente(HttpRequest requisicao) => requisicao.Headers["X-Requested-With"] == "nap-spa";

    private static CookieOptions Opcoes(DateTimeOffset? expiraEm) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Caminho,
        Expires = expiraEm,
    };
}
