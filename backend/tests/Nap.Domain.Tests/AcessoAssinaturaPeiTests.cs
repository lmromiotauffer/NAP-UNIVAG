using Nap.Domain.Comum;
using Nap.Domain.Peis;

namespace Nap.Domain.Tests;

public class AcessoAssinaturaPeiTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Versao = Guid.CreateVersion7();
    private static readonly Guid Autor = Guid.CreateVersion7();
    private static readonly string Hash = new('a', 64);

    private static (AcessoAssinaturaPei acesso, string token, string codigo) Gerar() =>
        AcessoAssinaturaPei.Gerar(Versao, Hash, Autor, Agora);

    private static string Errado(string codigo) => codigo == "000000" ? "000001" : "000000";

    [Fact]
    public void Acesso_expira_em_72h()
    {
        var (a, token, codigo) = Gerar();

        Assert.Equal((Versao, Hash, Autor, Agora), (a.VersaoId, a.HashConteudo, a.CriadoPorId, a.CriadoEm));
        Assert.Equal(Agora.AddHours(72), a.ExpiraEm);
        Assert.True(a.Valido(Agora.AddHours(72).AddTicks(-1)));
        Assert.False(a.Valido(Agora.AddHours(72)));
        Assert.IsType<ResultadoCodigo.Invalidado>(a.VerificarCodigo(token, codigo, Agora.AddHours(72)));
        Assert.Equal(0, a.Tentativas);
    }

    [Fact]
    public void Quinta_tentativa_errada_invalida()
    {
        var (a, token, codigo) = Gerar();

        for (var restantes = 4; restantes >= 1; restantes--)
            Assert.Equal(new ResultadoCodigo.Errado(restantes), a.VerificarCodigo(token, Errado(codigo), Agora));
        Assert.True(a.Valido(Agora));
        Assert.IsType<ResultadoCodigo.Correto>(a.VerificarCodigo(token, codigo, Agora));

        Assert.IsType<ResultadoCodigo.Invalidado>(a.VerificarCodigo(token, Errado(codigo), Agora));

        Assert.Equal(5, a.Tentativas);
        Assert.Equal(Agora, a.InvalidadoEm);
        Assert.False(a.Valido(Agora));
        Assert.IsType<ResultadoCodigo.Invalidado>(a.VerificarCodigo(token, codigo, Agora));
    }

    [Fact]
    public void Uso_unico()
    {
        var (a, token, codigo) = Gerar();
        Assert.IsType<ResultadoCodigo.Correto>(a.VerificarCodigo(token, codigo, Agora));
        Assert.True(a.Valido(Agora));

        a.MarcarUsado(Agora);

        Assert.Equal(Agora, a.UsadoEm);
        Assert.False(a.Valido(Agora));
        Assert.IsType<ResultadoCodigo.Invalidado>(a.VerificarCodigo(token, codigo, Agora));
        Assert.Throws<DomainException>(() => a.MarcarUsado(Agora));
    }

    [Fact]
    public void Invalidar_encerra_o_acesso()
    {
        var (a, token, codigo) = Gerar();

        a.Invalidar(Agora);
        a.Invalidar(Agora.AddHours(1));

        Assert.Equal(Agora, a.InvalidadoEm);
        Assert.False(a.Valido(Agora));
        Assert.IsType<ResultadoCodigo.Invalidado>(a.VerificarCodigo(token, codigo, Agora));
        Assert.Throws<DomainException>(() => a.MarcarUsado(Agora));
    }

    [Fact]
    public void Gerar_exige_hash_do_conteudo()
    {
        Assert.Throws<DomainException>(() => AcessoAssinaturaPei.Gerar(Versao, " ", Autor, Agora));
        Assert.Throws<DomainException>(() => AcessoAssinaturaPei.Gerar(Versao, null!, Autor, Agora));
    }

    [Fact]
    public void Codigo_tem_6_digitos_e_token_nao_e_guardado_em_claro()
    {
        var (a, token, codigo) = Gerar();
        var (b, outroToken, _) = Gerar();

        Assert.Matches("^[0-9]{6}$", codigo);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);
        Assert.NotEqual(token, outroToken);
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(AcessoAssinaturaPei.HashDe(token), a.TokenHash);
        Assert.NotEqual(AcessoAssinaturaPei.HashDe(codigo), a.CodigoHash);
        Assert.Equal(AcessoAssinaturaPei.HashDe(token + ":" + codigo), a.CodigoHash);
        Assert.IsType<ResultadoCodigo.Errado>(a.VerificarCodigo(outroToken, codigo, Agora));
        Assert.Matches("^[0-9a-f]{64}$", a.TokenHash);
        Assert.NotEqual(token, a.TokenHash);
        Assert.NotEqual(codigo, a.CodigoHash);
        Assert.Equal(AcessoAssinaturaPei.HashDe("abc"), AcessoAssinaturaPei.HashDe("abc"));
    }
}
