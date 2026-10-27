using Nap.Application.Comum;
using Nap.Domain.Peis;

namespace Nap.Application.Features.Peis;

/// <summary>Abertura do acesso temporário do aluno, comum a ver a versão e a assinar (endpoints anônimos).</summary>
public static class AcessoAluno
{
    public const string AvisoAssinatura = "Assinatura demonstrativa — não certificada (pendência institucional)";

    // Cada conflito significa que outra requisição gravou o acesso; essas gravações são limitadas pelas 5 tentativas.
    private const int LimiteRegravacoes = 10;

    /// <summary>Token desconhecido, expirado, usado, invalidado ou de versão que mudou: o mesmo 404, sem oráculo.</summary>
    public static NotFoundException LinkInvalido() => new NotFoundException("Acesso", "Link inválido ou expirado.");

    /// <summary>
    /// Localiza o acesso pelo token e confere o código. Código errado grava a tentativa antes do 400 com
    /// <c>SalvarAsync</c> avulso: dentro de uma transação, o rollback desfaria a contagem.
    /// </summary>
    public static async Task<(AcessoAssinaturaPei acesso, Pei pei, VersaoPei versao)> AbrirAsync(
        IAcessoAssinaturaRepository acessos, IPeiRepository peis, IUnidadeDeTrabalho uow, string token, string codigo,
        DateTimeOffset agora, CancellationToken ct)
    {
        var acesso = await acessos.ObterPorTokenHashAsync(AcessoAssinaturaPei.HashDe(token), ct);
        if (acesso is null || !acesso.Valido(agora)) throw LinkInvalido();
        var pei = await peis.ObterPorVersaoAsync(acesso.VersaoId, ct) ?? throw LinkInvalido();
        var versao = pei.Versoes.Single(v => v.Id == acesso.VersaoId);
        if (versao.Estado != EstadoVersaoPei.AguardandoAssinaturas
            || !string.Equals(versao.HashConteudo, acesso.HashConteudo, StringComparison.Ordinal)
            || versao.AssinaturaValida(PapelAssinatura.Aluno) is not null)
            throw LinkInvalido();

        for (var regravacoes = 0; ; regravacoes++)
        {
            var resultado = acesso.VerificarCodigo(token, codigo, agora);
            if (resultado is ResultadoCodigo.Correto) return (acesso, pei, versao);
            try
            {
                await uow.SalvarAsync(ct);
            }
            catch (ConflictException) when (regravacoes < LimiteRegravacoes)
            {
                // Tentativa simultânea gravou antes: recontar sobre o valor atual, senão a corrida pularia a contagem.
                if (!await acessos.RecarregarAsync(acesso, ct)) throw LinkInvalido();
                continue;
            }
            throw resultado is ResultadoCodigo.Errado errado
                ? Validacao.ErroDeCampo("codigo", $"Código incorreto. Restam {errado.Restantes} tentativas.")
                : LinkInvalido();
        }
    }
}
