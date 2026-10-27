using Nap.Application.Features.Comunicacoes;
using Nap.Application.Features.Comunicacoes.Commands;
using Nap.Domain.Comunicacoes;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class MensagemConcorrenciaTests(SqlServerFixture fx)
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Roda <paramref name="entre"/> logo depois da leitura: simula o outro participante gravando nesse intervalo.</summary>
    private sealed class RepositorioComIntervalo(MensagemRepository real, Func<Task> entre) : IMensagemRepository
    {
        public async Task<Mensagem?> ObterAsync(Guid id, CancellationToken ct)
        {
            var m = await real.ObterAsync(id, ct);
            await entre();
            return m;
        }

        public void Adicionar(Mensagem m) => real.Adicionar(m);
        public void Remover(Mensagem m) => real.Remover(m);
        public Task<bool> RecarregarAsync(Mensagem m, CancellationToken ct) => real.RecarregarAsync(m, ct);
    }

    [Fact]
    public async Task Autor_arquiva_enquanto_o_destinatario_le_sem_falso_conflito()
    {
        var ct = TestContext.Current.CancellationToken;
        var autor = await fx.CriarProfissionalAsync();
        var destinatario = await fx.CriarProfissionalAsync();
        var m = Mensagem.CriarRascunho(autor, TipoMensagem.Aviso, "Assunto", "Corpo", destinatario, null, null, null, Agora);
        m.Enviar(Agora);
        m.LimparEventos();
        await using (var db = fx.CriarContexto())
        {
            db.Mensagens.Add(m);
            await db.SaveChangesAsync(ct);
        }

        async Task DestinatarioLe()
        {
            await using var outro = fx.CriarContexto();
            var lida = (await new MensagemRepository(outro).ObterAsync(m.Id, ct))!;
            lida.MarcarLida(destinatario, Agora);
            await outro.SaveChangesAsync(ct);
        }

        await using (var db = fx.CriarContexto())
        {
            var repo = new RepositorioComIntervalo(new MensagemRepository(db), DestinatarioLe);
            await new ArquivarMensagemHandler(repo, new AlunoRepository(db), new EncaminhamentoRepository(db), new UsuarioAtualTeste(autor),
                    new RelogioTeste(Agora), new UnidadeDeTrabalho(db))
                .Handle(new ArquivarMensagemCommand(m.Id), ct);
        }

        await using var leitura = fx.CriarContexto();
        var final = (await new MensagemRepository(leitura).ObterAsync(m.Id, ct))!;
        Assert.Equal((Agora, Agora), (final.ArquivadaPeloAutorEm, final.LidaEm));
    }
}
