namespace Nap.Application.Comum;

/// <summary>Armazenamento privado de arquivos. A chave é opaca (gerada aqui, nunca derivada do nome do arquivo).</summary>
public interface IArmazenamentoArquivos
{
    Task<string> SalvarAsync(Stream conteudo, CancellationToken ct);

    /// <exception cref="NotFoundException">Chave sem arquivo.</exception>
    Task<Stream> AbrirAsync(string chave, CancellationToken ct);

    /// <summary>Não lança se o arquivo já não existir.</summary>
    Task RemoverAsync(string chave, CancellationToken ct);
}
