using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Domain.Comunicacoes;
using Nap.Domain.Encaminhamentos;
using Nap.Domain.Notificacoes;
using Nap.Infrastructure.Persistencia;

namespace Nap.Infrastructure.Tests;

// Mesma coleção do ProcessadorOutboxTests: ele processaria a linha que estes testes conferem como pendente.
[Collection("Outbox")]
public class OutboxTests(SqlServerFixture fx)
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(-4));

    internal static async Task<Encaminhamento> NovoEncaminhamentoAsync(SqlServerFixture fx)
    {
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        return Encaminhamento.Criar(aluno.Id, curso, prof, Especialidade.Psicologia, DestinoEncaminhamento.Sip,
            new DateOnly(2026, 10, 1), "Motivo restrito", "Resumo compartilhável", null, new DateOnly(2026, 10, 2), Agora);
    }

    [Fact]
    public async Task Salvar_encaminhamento_grava_linha_na_outbox_na_mesma_transacao()
    {
        var ct = TestContext.Current.CancellationToken;
        var enc = await NovoEncaminhamentoAsync(fx);
        var eventoId = enc.Eventos.Single().EventoId;

        await using (var db = fx.CriarContexto())
        {
            var uow = new UnidadeDeTrabalho(db);
            await uow.EmTransacaoAsync(async tct =>
            {
                db.Add(enc);
                await uow.SalvarAsync(tct);
                return enc.Id;
            }, ct);
        }
        Assert.Empty(enc.Eventos);

        await using var leitura = fx.CriarContexto();
        var linha = await leitura.Outbox.SingleAsync(m => m.Id == eventoId, ct);
        Assert.Equal("EncaminhamentoCriado", linha.Tipo);
        Assert.Null(linha.ProcessadoEm);
        Assert.Equal(0, linha.Tentativas);
        Assert.Contains("\"Psicologia\"", linha.Conteudo);
        var evento = JsonSerializer.Deserialize<EncaminhamentoCriado>(linha.Conteudo, MensagemOutbox.Json)!;
        Assert.Equal(eventoId, evento.EventoId);
        Assert.Equal(enc.Id, evento.EncaminhamentoId);
        Assert.Equal(DestinoEncaminhamento.Sip, evento.Destino);
        Assert.Equal(Agora, evento.OcorridoEm);
    }

    [Fact]
    public void Retorno_registrado_volta_do_json_com_os_enums()
    {
        var evento = new RetornoEncaminhamentoRegistrado(Guid.CreateVersion7(), Agora, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), EstadoEncaminhamento.EmAtendimento, AcaoRetorno.InicioAtendimento);
        var mensagem = new MensagemOutbox(evento);

        Assert.Equal("RetornoEncaminhamentoRegistrado", mensagem.Tipo);
        Assert.Contains("\"EmAtendimento\"", mensagem.Conteudo);
        Assert.Equal(evento, JsonSerializer.Deserialize<RetornoEncaminhamentoRegistrado>(mensagem.Conteudo, MensagemOutbox.Json));
    }

    [Fact]
    public async Task Enviar_mensagem_grava_linha_na_outbox_que_volta_do_json()
    {
        var ct = TestContext.Current.CancellationToken;
        var autor = await fx.CriarProfissionalAsync();
        var destinatario = await fx.CriarProfissionalAsync();
        var mensagem = Mensagem.CriarRascunho(autor, TipoMensagem.SolicitacaoInformacao, "Assunto", "Corpo", destinatario,
            null, null, null, Agora);
        mensagem.Enviar(Agora);
        var eventoId = mensagem.Eventos.Single().EventoId;

        await using (var db = fx.CriarContexto())
        {
            db.Add(mensagem);
            await new UnidadeDeTrabalho(db).SalvarAsync(ct);
        }
        Assert.Empty(mensagem.Eventos);

        await using var leitura = fx.CriarContexto();
        var linha = await leitura.Outbox.SingleAsync(m => m.Id == eventoId, ct);
        Assert.Equal("MensagemEnviada", linha.Tipo);
        Assert.Null(linha.ProcessadoEm);
        Assert.Contains("\"SolicitacaoInformacao\"", linha.Conteudo);
        var evento = JsonSerializer.Deserialize<MensagemEnviada>(linha.Conteudo, MensagemOutbox.Json)!;
        Assert.Equal(new MensagemEnviada(eventoId, Agora, mensagem.Id, autor, destinatario, TipoMensagem.SolicitacaoInformacao), evento);
    }

    [Fact]
    public async Task Rollback_nao_deixa_linha_na_outbox()
    {
        var ct = TestContext.Current.CancellationToken;
        var enc = await NovoEncaminhamentoAsync(fx);
        var eventoId = enc.Eventos.Single().EventoId;

        await using (var db = fx.CriarContexto())
        {
            var uow = new UnidadeDeTrabalho(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() => uow.EmTransacaoAsync<Guid>(async tct =>
            {
                db.Add(enc);
                await uow.SalvarAsync(tct);
                throw new InvalidOperationException("falha depois de salvar");
            }, ct));
        }

        await using var leitura = fx.CriarContexto();
        Assert.False(await leitura.Outbox.AnyAsync(m => m.Id == eventoId, ct));
        Assert.False(await leitura.Set<Encaminhamento>().AnyAsync(e => e.Id == enc.Id, ct));
    }

    [Fact]
    public async Task Notificacao_duplicada_para_o_mesmo_evento_e_destinatario_viola_unicidade()
    {
        var ct = TestContext.Current.CancellationToken;
        var destinatario = await fx.CriarProfissionalAsync();
        var eventoId = Guid.CreateVersion7();
        await using (var db = fx.CriarContexto())
        {
            db.Notificacoes.Add(Notificacao.Criar(destinatario, eventoId, "EncaminhamentoCriado", "Novo encaminhamento",
                "Texto", "/encaminhamentos/1", Agora));
            await new UnidadeDeTrabalho(db).SalvarAsync(ct);
        }

        await using var outro = fx.CriarContexto();
        outro.Notificacoes.Add(Notificacao.Criar(destinatario, eventoId, "EncaminhamentoCriado", "Novo encaminhamento",
            "Texto", "/encaminhamentos/1", Agora));
        var ex = await Assert.ThrowsAsync<ConflictException>(() => new UnidadeDeTrabalho(outro).SalvarAsync(ct));
        Assert.Equal("Notificação já registrada", ex.Message);
    }
}
