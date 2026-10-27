using Nap.Domain.Comunicacoes;
using Nap.Domain.Notificacoes;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Tests;

public class NotificacaoQueriesTests(SqlServerFixture fx)
{
    [Fact]
    public async Task Contagem_nao_soma_nova_mensagem_que_ja_conta_como_mensagem_nao_lida()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await fx.CriarProfissionalAsync();
        var agora = DateTimeOffset.UtcNow;
        await using (var db = fx.CriarContexto())
        {
            db.Notificacoes.AddRange(
                Notificacao.Criar(usuario, Guid.NewGuid(), nameof(MensagemEnviada), "Nova mensagem", "Assunto", "/comunicacoes/1", agora),
                Notificacao.Criar(usuario, Guid.NewGuid(), "EncaminhamentoCriado", "Novo encaminhamento", "Texto", "/encaminhamentos/1", agora));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        Assert.Equal(1, await new NotificacaoQueries(leitura).ContarNaoLidasAsync(usuario, ct));
    }
}
