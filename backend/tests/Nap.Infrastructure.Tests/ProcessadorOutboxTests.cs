using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nap.Application.Comum;
using Nap.Application.Features.Notificacoes.Commands;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comum;
using Nap.Domain.Encaminhamentos;
using Nap.Domain.Notificacoes;
using Nap.Infrastructure.Identidade;
using Nap.Infrastructure.Notificacoes;
using Nap.Infrastructure.Persistencia;

namespace Nap.Infrastructure.Tests;

/// <summary>
/// O processador varre todas as pendentes do banco compartilhado: na mesma coleção de <see cref="OutboxTests"/> (que confere
/// linhas ainda não processadas). As mensagens daqui têm <c>OcorridoEm</c> antigo, para entrar no primeiro lote, e as falhas
/// vêm de um tipo de evento desconhecido, para nunca contar tentativas em linhas de outros testes.
/// </summary>
[Collection("Outbox")]
public class ProcessadorOutboxTests(SqlServerFixture fx)
{
    private static readonly DateTimeOffset Antigo = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly RelogioTeste _relogio = new(new DateTimeOffset(2026, 10, 2, 15, 0, 0, TimeSpan.Zero));

    private sealed record EventoDesconhecido(Guid EventoId, DateTimeOffset OcorridoEm) : IEventoDominio;

    private sealed class EnvioEmailFixo : IEnvioEmail
    {
        public Task<EstadoEmail> EnviarAsync(Guid destinatarioId, string assunto, string corpo, CancellationToken ct) =>
            Task.FromResult(EstadoEmail.Enviado);
    }

    /// <summary>Todo SIP "ativo" é um id sem usuário: inserir a notificação viola a FK no meio do consumidor.</summary>
    private class UsuarioQueriesComSipInexistente : IUsuarioQueries
    {
        public Task<bool> EhProfissionalAtivoAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
        public Task<IReadOnlyList<ProfissionalDto>> ListarProfissionaisAsync(bool incluirInativos, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProfissionalDto>>([]);
        public Task<IReadOnlyList<ProfissionalProducaoDto>> ListarProfissionaisDaProducaoAsync(DateOnly inicio, DateOnly fim, TimeZoneInfo fuso,
            Guid? profissionalId, CancellationToken ct) => Task.FromResult<IReadOnlyList<ProfissionalProducaoDto>>([]);
        public virtual Task<IReadOnlyList<Guid>> ListarAtivosPorPerfilAsync(string perfil, Guid? cursoId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>([Guid.NewGuid()]);
        public Task<bool> EstaAtivoAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
        public Task<UsuarioResumoDto?> ObterResumoAsync(Guid id, CancellationToken ct) => Task.FromResult<UsuarioResumoDto?>(null);
        public Task<IReadOnlyList<UsuarioResumoDto>> ListarAtivosAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UsuarioResumoDto>>([]);
    }

    private ServiceProvider Servicos(IUsuarioQueries? usuarios = null)
    {
        var configuracao = new ConfigurationBuilder()
            // A string do Redis é exigida no registro; nada aqui resolve a conexão, então uma porta fechada basta.
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Nap"] = fx.ConnectionString, ["ConnectionStrings:Redis"] = "localhost:1" }).Build();
        var servicos = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuracao)
            .AdicionarApplication().AdicionarInfrastructure(configuracao)
            .AddSingleton<IRelogio>(_relogio).AddSingleton<IEnvioEmail, EnvioEmailFixo>();
        if (usuarios is not null) servicos.AddSingleton(usuarios);
        return servicos.BuildServiceProvider();
    }

    private ProcessadorOutbox Processador(ServiceProvider sp) =>
        new(sp.GetRequiredService<IServiceScopeFactory>(), _relogio, Options.Create(new OpcoesOutbox()), NullLogger<ProcessadorOutbox>.Instance);

    private async Task<(EncaminhamentoCriado Evento, Guid Sip)> EventoCriadoAsync(DateTimeOffset? ocorridoEm = null)
    {
        var sip = await fx.CriarProfissionalAsync(Perfis.Sip);
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof, "Joana Prado");
        var evento = new EncaminhamentoCriado(Guid.CreateVersion7(), ocorridoEm ?? Antigo, Guid.NewGuid(), aluno.Id, curso, prof,
            Especialidade.Psicologia, DestinoEncaminhamento.Sip);
        await GravarAsync(evento);
        return (evento, sip);
    }

    private async Task GravarAsync(IEventoDominio evento)
    {
        await using var db = fx.CriarContexto();
        db.Outbox.Add(new MensagemOutbox(evento));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<MensagemOutbox> MensagemAsync(Guid id)
    {
        await using var db = fx.CriarContexto();
        return await db.Outbox.SingleAsync(m => m.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<int> NotificacoesAsync(Guid eventoId)
    {
        await using var db = fx.CriarContexto();
        return await db.Notificacoes.CountAsync(n => n.EventoId == eventoId, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Processa_pendente_e_marca_processada()
    {
        var ct = TestContext.Current.CancellationToken;
        var (evento, sip) = await EventoCriadoAsync();
        await using var sp = Servicos();

        Assert.True(await Processador(sp).ProcessarPendentesAsync(ct) > 0);

        var mensagem = await MensagemAsync(evento.EventoId);
        Assert.Equal(_relogio.Agora, mensagem.ProcessadoEm);
        Assert.Equal(1, mensagem.Tentativas); // a tentativa é contada na reserva, com ou sem sucesso
        await using var db = fx.CriarContexto();
        var n = await db.Notificacoes.SingleAsync(n => n.EventoId == evento.EventoId && n.DestinatarioId == sip, ct);
        Assert.Equal("Novo encaminhamento: Joana Prado", n.Titulo);
        Assert.Equal(EstadoEmail.Enviado, n.EstadoEmail);
    }

    [Fact]
    public async Task Duas_instancias_ao_mesmo_tempo_processam_a_mensagem_uma_vez_sem_falha()
    {
        var ct = TestContext.Current.CancellationToken;
        var (evento, sip) = await EventoCriadoAsync();
        await using var sp1 = Servicos();
        await using var sp2 = Servicos();

        await Task.WhenAll(Processador(sp1).ProcessarPendentesAsync(ct), Processador(sp2).ProcessarPendentesAsync(ct));

        var mensagem = await MensagemAsync(evento.EventoId);
        // Uma só tentativa (a da reserva) e sem falha registrada: a segunda instância não tentou de novo (antes, batia no índice único).
        Assert.Equal((_relogio.Agora, 1, (string?)null), (mensagem.ProcessadoEm, mensagem.Tentativas, mensagem.UltimoErro));
        await using var db = fx.CriarContexto();
        Assert.Equal(1, await db.Notificacoes.CountAsync(n => n.EventoId == evento.EventoId && n.DestinatarioId == sip, ct));
    }

    [Fact]
    public async Task Falha_agenda_nova_tentativa_com_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        var evento = new EventoDesconhecido(Guid.CreateVersion7(), Antigo);
        await GravarAsync(evento);
        await using var sp = Servicos();

        await Processador(sp).ProcessarPendentesAsync(ct);

        var mensagem = await MensagemAsync(evento.EventoId);
        Assert.Null(mensagem.ProcessadoEm);
        Assert.Equal(1, mensagem.Tentativas);
        Assert.Equal(_relogio.Agora.AddSeconds(30), mensagem.ProximaTentativaEm);
        Assert.Contains("EventoDesconhecido", mensagem.UltimoErro);

        // Antes do prazo, não é tentada de novo.
        _relogio.Agora = _relogio.Agora.AddSeconds(29);
        await Processador(sp).ProcessarPendentesAsync(ct);
        Assert.Equal(1, (await MensagemAsync(evento.EventoId)).Tentativas);
    }

    /// <summary>Lê a tentativa gravada na linha da outbox no meio do consumidor e então falha, como um processo que cai ali.</summary>
    private sealed class UsuarioQueriesQueLeATentativa(SqlServerFixture fx, Guid mensagemId) : UsuarioQueriesComSipInexistente
    {
        public int? TentativasDuranteOConsumo { get; private set; }

        public override async Task<IReadOnlyList<Guid>> ListarAtivosPorPerfilAsync(string perfil, Guid? cursoId, CancellationToken ct)
        {
            await using var db = fx.CriarContexto();
            TentativasDuranteOConsumo = await db.Outbox.Where(m => m.Id == mensagemId).Select(m => m.Tentativas).SingleAsync(ct);
            throw new InvalidOperationException("Consumidor caiu");
        }
    }

    [Fact]
    public async Task Tentativa_e_contada_na_reserva_antes_de_o_consumidor_falhar()
    {
        var ct = TestContext.Current.CancellationToken;
        var (evento, _) = await EventoCriadoAsync();
        var usuarios = new UsuarioQueriesQueLeATentativa(fx, evento.EventoId);
        await using var sp = Servicos(usuarios);

        await Processador(sp).ProcessarPendentesAsync(ct);

        // Já valia 1 quando o consumidor rodou: se o processo morresse ali, a tentativa não se perderia. O catch não soma outra.
        Assert.Equal(1, usuarios.TentativasDuranteOConsumo);
        var mensagem = await MensagemAsync(evento.EventoId);
        Assert.Equal((1, _relogio.Agora.AddSeconds(30)), (mensagem.Tentativas, mensagem.ProximaTentativaEm));
        Assert.Contains("Consumidor caiu", mensagem.UltimoErro);
    }

    [Fact]
    public async Task Falha_no_meio_do_consumidor_descarta_o_parcial_e_segue_o_lote()
    {
        var ct = TestContext.Current.CancellationToken;
        var (falha, _) = await EventoCriadoAsync(Antigo.AddDays(-1)); // processada antes da válida
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        var valida = new RetornoEncaminhamentoRegistrado(Guid.CreateVersion7(), Antigo, Guid.NewGuid(), aluno.Id, prof,
            Guid.NewGuid(), EstadoEncaminhamento.Acolhido, AcaoRetorno.Acolhimento);
        await GravarAsync(valida);
        await using var sp = Servicos(new UsuarioQueriesComSipInexistente());

        await Processador(sp).ProcessarPendentesAsync(ct);

        var mensagem = await MensagemAsync(falha.EventoId);
        Assert.Null(mensagem.ProcessadoEm);
        Assert.Equal(1, mensagem.Tentativas);
        Assert.Equal(_relogio.Agora.AddSeconds(30), mensagem.ProximaTentativaEm);
        Assert.Contains("DbUpdateException", mensagem.UltimoErro);
        Assert.Equal(0, await NotificacoesAsync(falha.EventoId));
        Assert.NotNull((await MensagemAsync(valida.EventoId)).ProcessadoEm);
        Assert.Equal(1, await NotificacoesAsync(valida.EventoId));
    }

    [Fact]
    public async Task Apos_oito_falhas_para_de_tentar()
    {
        var ct = TestContext.Current.CancellationToken;
        var evento = new EventoDesconhecido(Guid.CreateVersion7(), Antigo);
        await GravarAsync(evento);
        await using var sp = Servicos();

        for (var i = 0; i < 10; i++)
        {
            await Processador(sp).ProcessarPendentesAsync(ct);
            _relogio.Agora = _relogio.Agora.AddHours(2);
        }

        var mensagem = await MensagemAsync(evento.EventoId);
        Assert.Equal(MensagemOutbox.MaximoTentativas, mensagem.Tentativas);
        Assert.Null(mensagem.ProcessadoEm);
    }

    [Fact]
    public async Task Queda_entre_notificar_e_marcar_nao_duplica()
    {
        var ct = TestContext.Current.CancellationToken;
        var (evento, _) = await EventoCriadoAsync();
        await using var sp = Servicos();
        await using (var escopo = sp.CreateAsyncScope())
            await escopo.ServiceProvider.GetRequiredService<GerarNotificacoesHandler>().Handle(evento, ct);
        var antes = await NotificacoesAsync(evento.EventoId);
        Assert.True(antes > 0);

        await Processador(sp).ProcessarPendentesAsync(ct);

        Assert.Equal(antes, await NotificacoesAsync(evento.EventoId));
        Assert.NotNull((await MensagemAsync(evento.EventoId)).ProcessadoEm);
    }

    [Fact]
    public async Task Purga_tokens_expirados_ha_mais_de_sete_dias_chaves_com_mais_de_um_dia_e_outbox_entregue_ha_mais_de_trinta()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await fx.CriarProfissionalAsync();
        var agora = _relogio.Agora;
        var velho = new TokenRefresh(Guid.CreateVersion7(), usuario, Guid.NewGuid(), ServicoTokens.Hash(Guid.NewGuid().ToString()),
            agora.AddDays(-8), "carimbo");
        var recente = new TokenRefresh(Guid.CreateVersion7(), usuario, Guid.NewGuid(), ServicoTokens.Hash(Guid.NewGuid().ToString()),
            agora.AddDays(-6), "carimbo");
        await using (var db = fx.CriarContexto())
        {
            db.TokensRefresh.AddRange(velho, recente);
            await db.SaveChangesAsync(ct);
            await new ChavesIdempotencia(db, new RelogioTeste(agora.AddDays(-2))).ReservarAsync(usuario, "velha", "POST /x", ct);
            await new ChavesIdempotencia(db, _relogio).ReservarAsync(usuario, "nova", "POST /x", ct);
        }
        var (entregueHaMuito, _) = await EventoCriadoAsync();
        var (entregueRecente, _) = await EventoCriadoAsync();
        await using (var db = fx.CriarContexto())
        {
            await db.Outbox.Where(m => m.Id == entregueHaMuito.EventoId).ExecuteUpdateAsync(x => x.SetProperty(m => m.ProcessadoEm, agora.AddDays(-31)), ct);
            await db.Outbox.Where(m => m.Id == entregueRecente.EventoId).ExecuteUpdateAsync(x => x.SetProperty(m => m.ProcessadoEm, agora.AddDays(-1)), ct);
        }
        await using var sp = Servicos();

        // Banco compartilhado entre os testes: outras linhas antigas também podem cair, por isso >= 3.
        Assert.True(await Processador(sp).PurgarAsync(ct) >= 3);

        await using var leitura = fx.CriarContexto();
        var tokens = await leitura.TokensRefresh.Where(t => t.UsuarioId == usuario).Select(t => t.Id).ToListAsync(ct);
        Assert.Equal(new[] { recente.Id }, tokens);
        var chaves = await leitura.ChavesIdempotencia.Where(c => c.UsuarioId == usuario).Select(c => c.Chave).ToListAsync(ct);
        Assert.Equal(new[] { "nova" }, chaves);
        Assert.False(await leitura.Outbox.AnyAsync(m => m.Id == entregueHaMuito.EventoId, ct));
        Assert.True(await leitura.Outbox.AnyAsync(m => m.Id == entregueRecente.EventoId, ct));
    }
}
