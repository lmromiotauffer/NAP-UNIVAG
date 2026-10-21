using Microsoft.Extensions.Logging;
using Nap.Application.Comum;
using Nap.Infrastructure.Arquivos;

namespace Nap.Infrastructure.Tests;

public sealed class ArmazenamentoEmDiscoTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "nap-arquivos-" + Guid.NewGuid().ToString("N"));
    private readonly LoggerCaptura _logger = new();
    private readonly ArmazenamentoEmDisco _armazenamento;

    public ArmazenamentoEmDiscoTests() => _armazenamento = new ArmazenamentoEmDisco(_raiz, _logger);

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    [Fact]
    public async Task Salva_e_abre_o_mesmo_conteudo()
    {
        var ct = TestContext.Current.CancellationToken;
        byte[] dados = [1, 2, 3, 4, 5];

        var chave = await _armazenamento.SalvarAsync(new MemoryStream(dados), ct);

        Assert.Matches("^[0-9a-f]{32}$", chave);
        Assert.True(File.Exists(Path.Combine(_raiz, chave[^2..], chave)));
        await using var lido = await _armazenamento.AbrirAsync(chave, ct);
        var copia = new MemoryStream();
        await lido.CopyToAsync(copia, ct);
        Assert.Equal(dados, copia.ToArray());
    }

    [Fact]
    public async Task Cada_arquivo_recebe_uma_chave_distinta()
    {
        var ct = TestContext.Current.CancellationToken;
        var a = await _armazenamento.SalvarAsync(new MemoryStream([1]), ct);
        var b = await _armazenamento.SalvarAsync(new MemoryStream([1]), ct);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task Chave_com_travessia_e_rejeitada()
    {
        var ct = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<ArgumentException>(() => _armazenamento.AbrirAsync("../x", ct));
        await Assert.ThrowsAsync<ArgumentException>(() => _armazenamento.RemoverAsync("../x", ct));
        await Assert.ThrowsAsync<ArgumentException>(() => _armazenamento.AbrirAsync("ABCDEF0123456789ABCDEF0123456789", ct));
    }

    [Fact]
    public async Task Abrir_inexistente_lanca_nao_encontrado()
    {
        var ct = TestContext.Current.CancellationToken;
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _armazenamento.AbrirAsync(Guid.CreateVersion7().ToString("N"), ct));
        Assert.Equal("Arquivo não encontrado.", ex.Message);
    }

    [Fact]
    public async Task Remover_inexistente_nao_lanca()
    {
        var ct = TestContext.Current.CancellationToken;
        await _armazenamento.RemoverAsync(Guid.CreateVersion7().ToString("N"), ct);

        var chave = await _armazenamento.SalvarAsync(new MemoryStream([1]), ct);
        await _armazenamento.RemoverAsync(chave, ct);
        await _armazenamento.RemoverAsync(chave, ct);
        await Assert.ThrowsAsync<NotFoundException>(() => _armazenamento.AbrirAsync(chave, ct));
    }

    [Fact]
    public async Task Falha_ao_remover_e_logada_e_relancada()
    {
        // Arquivo aberto só bloqueia a exclusão no Windows.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Bloqueio de exclusão por arquivo aberto só existe no Windows.");
        var ct = TestContext.Current.CancellationToken;
        var chave = await _armazenamento.SalvarAsync(new MemoryStream([1]), ct);
        await using var aberto = await _armazenamento.AbrirAsync(chave, ct);

        await Assert.ThrowsAsync<IOException>(() => _armazenamento.RemoverAsync(chave, ct));

        var (nivel, mensagem) = Assert.Single(_logger.Registros);
        Assert.Equal(LogLevel.Warning, nivel);
        Assert.Contains(chave, mensagem);
    }

    private sealed class LoggerCaptura : ILogger<ArmazenamentoEmDisco>
    {
        public List<(LogLevel Nivel, string Mensagem)> Registros { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Registros.Add((logLevel, formatter(state, exception)));
    }
}
