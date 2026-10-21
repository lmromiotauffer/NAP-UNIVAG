using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Nap.Domain.Comum;

namespace Nap.Domain.Peis;

public abstract record ResultadoCodigo
{
    public sealed record Correto : ResultadoCodigo;
    public sealed record Errado(int Restantes) : ResultadoCodigo;
    public sealed record Invalidado : ResultadoCodigo;
}

// Token e código só existem em claro no retorno de Gerar; aqui ficam apenas os hashes.
// O hash do código inclui o token, para que um dump do banco não permita testar os 10^6 códigos isoladamente.
public class AcessoAssinaturaPei : Entidade
{
    private const int MaximoTentativas = 5;
    private static readonly TimeSpan Validade = TimeSpan.FromHours(72);

    public Guid VersaoId { get; private set; }
    public string HashConteudo { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public string CodigoHash { get; private set; } = null!;
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset ExpiraEm { get; private set; }
    public DateTimeOffset? UsadoEm { get; private set; }
    public DateTimeOffset? InvalidadoEm { get; private set; }
    public int Tentativas { get; private set; }
    public Guid CriadoPorId { get; private set; }

    private AcessoAssinaturaPei() { } // ORM

    public static (AcessoAssinaturaPei acesso, string token, string codigo) Gerar(Guid versaoId, string hashConteudo,
        Guid autorId, DateTimeOffset agora)
    {
        if (string.IsNullOrWhiteSpace(hashConteudo))
            throw new DomainException("O hash do conteúdo é obrigatório para gerar o acesso.");
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var codigo = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
        var acesso = new AcessoAssinaturaPei
        {
            VersaoId = versaoId,
            HashConteudo = hashConteudo,
            TokenHash = HashDe(token),
            CodigoHash = HashDoCodigo(token, codigo),
            CriadoEm = agora,
            ExpiraEm = agora + Validade,
            CriadoPorId = autorId,
        };
        return (acesso, token, codigo);
    }

    public bool Valido(DateTimeOffset agora) => UsadoEm is null && InvalidadoEm is null && agora < ExpiraEm;

    // Verificar não consome o acesso: abrir a versão confere o código, assinar chama MarcarUsado.
    public ResultadoCodigo VerificarCodigo(string token, string codigo, DateTimeOffset agora)
    {
        if (!Valido(agora)) return new ResultadoCodigo.Invalidado();
        var informado = Convert.FromHexString(HashDoCodigo(token ?? string.Empty, codigo ?? string.Empty));
        if (CryptographicOperations.FixedTimeEquals(informado, Convert.FromHexString(CodigoHash)))
            return new ResultadoCodigo.Correto();
        Tentativas++;
        if (Tentativas < MaximoTentativas) return new ResultadoCodigo.Errado(MaximoTentativas - Tentativas);
        InvalidadoEm = agora;
        return new ResultadoCodigo.Invalidado();
    }

    public void MarcarUsado(DateTimeOffset agora)
    {
        if (!Valido(agora)) throw new DomainException("O acesso não está mais válido.");
        UsadoEm = agora;
    }

    public void Invalidar(DateTimeOffset agora) => InvalidadoEm ??= agora;

    private static string HashDoCodigo(string token, string codigo) => HashDe(token + ":" + codigo);

    public static string HashDe(string segredo) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(segredo)));
}
