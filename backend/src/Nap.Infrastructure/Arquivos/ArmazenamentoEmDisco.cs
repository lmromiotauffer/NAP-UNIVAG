using Microsoft.Extensions.Logging;
using Nap.Application.Comum;

namespace Nap.Infrastructure.Arquivos;

/// <summary>Grava em <c>{raiz}/{chave[^2..]}/{chave}</c> (fim aleatório do Guid v7; o início é o timestamp); a raiz (absoluta) fica fora de qualquer raiz web.</summary>
public sealed class ArmazenamentoEmDisco(string raiz, ILogger<ArmazenamentoEmDisco> logger) : IArmazenamentoArquivos
{
    /// <summary>Cria a raiz e grava/apaga um arquivo de teste: pasta errada ou sem permissão falha na partida, não no primeiro upload.</summary>
    public void VerificarGravacao()
    {
        Directory.CreateDirectory(raiz);
        var teste = Path.Combine(raiz, $".teste-gravacao-{Guid.NewGuid():N}");
        File.WriteAllBytes(teste, []);
        File.Delete(teste);
    }

    public async Task<string> SalvarAsync(Stream conteudo, CancellationToken ct)
    {
        var chave = Guid.CreateVersion7().ToString("N");
        var caminho = Caminho(chave);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        // CreateNew: um arquivo existente nunca é sobrescrito (nem apagado pelo catch abaixo).
        var destino = new FileStream(caminho, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        try
        {
            await using (destino)
                await conteudo.CopyToAsync(destino, ct);
        }
        catch
        {
            // Gravação interrompida: ninguém conhece a chave, então o arquivo parcial seria lixo.
            // Falha ao apagar não mascara o erro original.
            try { File.Delete(caminho); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
        return chave;
    }

    public Task<Stream> AbrirAsync(string chave, CancellationToken ct)
    {
        var caminho = Caminho(chave);
        try
        {
            return Task.FromResult<Stream>(new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new NotFoundException("Arquivo");
        }
    }

    public Task RemoverAsync(string chave, CancellationToken ct)
    {
        var caminho = Caminho(chave);
        try
        {
            if (File.Exists(caminho)) File.Delete(caminho);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Só a chave: o log nunca recebe dado do aluno.
            logger.LogWarning(e, "Falha ao remover o arquivo {Chave}", chave);
            throw;
        }
        return Task.CompletedTask;
    }

    // Só hexadecimal minúsculo de 32 caracteres (Guid "N"): impede travessia de diretório.
    private string Caminho(string chave)
    {
        if (chave is not { Length: 32 } || !chave.All(char.IsAsciiHexDigitLower))
            throw new ArgumentException("Chave de arquivo inválida.", nameof(chave));
        return Path.Combine(raiz, chave[^2..], chave);
    }
}
