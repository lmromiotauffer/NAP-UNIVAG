using Nap.Application.Comum;

namespace Nap.Application.Tests.Fakes;

public sealed class ArmazenamentoEmMemoria(List<string>? ordem = null) : IArmazenamentoArquivos
{
    public Dictionary<string, byte[]> Arquivos { get; } = [];
    public int Gravacoes { get; private set; }

    public async Task<string> SalvarAsync(Stream conteudo, CancellationToken ct)
    {
        using var copia = new MemoryStream();
        await conteudo.CopyToAsync(copia, ct);
        var chave = Guid.NewGuid().ToString("N");
        Arquivos[chave] = copia.ToArray();
        Gravacoes++;
        return chave;
    }

    public Task<Stream> AbrirAsync(string chave, CancellationToken ct)
    {
        ordem?.Add("abrir");
        return Arquivos.TryGetValue(chave, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes, writable: false))
            : throw new NotFoundException("Arquivo");
    }

    public Task RemoverAsync(string chave, CancellationToken ct)
    {
        Arquivos.Remove(chave);
        return Task.CompletedTask;
    }
}
