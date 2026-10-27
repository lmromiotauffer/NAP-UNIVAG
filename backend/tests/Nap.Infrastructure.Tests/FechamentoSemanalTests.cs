using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Atendimentos.Commands;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Producao.Commands;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Producao;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Consultas;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class FechamentoSemanalTests(SqlServerFixture fx)
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Semana = new(2026, 9, 14);
    private static DateTimeOffset Terca(int hora) => new(2026, 9, 15, hora, 0, 0, TimeSpan.FromHours(-4));

    private static async Task FecharAsync(NapDbContext db, Guid prof, bool confirmar)
    {
        var usuario = new UsuarioAtualTeste(prof);
        var relogio = new RelogioTeste(Agora);
        await new FecharSemanaHandler(new FecharSemanaValidator(), new FechamentoRepository(db), new ProducaoQueries(db, relogio),
            new UsuarioQueries(db), usuario, relogio, AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo())
            .Handle(new FecharSemanaCommand(prof, Semana, confirmar), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Fechada_a_semana_corrigir_atendimento_dela_retorna_422_e_reabrir_libera()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var coord = await fx.CriarProfissionalAsync(Perfis.CoordenadorNap);
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, await fx.CriarCursoAsync(), prof);
        var atendimento = Atendimento.RegistrarAvulso(aluno.Id, prof, IntervaloHorario.Criar(Terca(10), Terca(11)),
            TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, new DadosClinicos("Demanda", null, null, null),
            "Atendimento sem agenda", prof, Terca(12));
        await using (var db = fx.CriarContexto())
        {
            db.Add(atendimento);
            await db.SaveChangesAsync(ct);
        }
        await using (var db = fx.CriarContexto()) await FecharAsync(db, prof, false);

        async Task CorrigirAsync()
        {
            await using var db = fx.CriarContexto();
            var versao = Convert.ToBase64String(await db.Atendimentos.Where(a => a.Id == atendimento.Id)
                .Select(a => EF.Property<byte[]>(a, "Versao")).SingleAsync(ct));
            var usuario = new UsuarioAtualTeste(prof);
            var relogio = new RelogioTeste(Agora);
            await new CorrigirAtendimentoHandler(new CorrigirAtendimentoValidator(), new AtendimentoRepository(db), new AgendaRepository(db),
                usuario, relogio, AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo(),
                new ServicoEscala(new EscalaRepository(db), new FeriadoRepository(db, relogio), relogio),
                new ServicoFechamento(new FechamentoRepository(db), relogio))
                .Handle(new CorrigirAtendimentoCommand(atendimento.Id, EstadoAtendimento.Realizado, TipoAtendimento.Devolutiva,
                    Terca(10), Terca(11), "Demanda revisada", null, null, null, null, versao), ct);
        }

        var ex = await Assert.ThrowsAsync<DomainException>(CorrigirAsync);
        Assert.Equal("A semana está fechada; peça a reabertura à coordenação.", ex.Message);

        await using (var db = fx.CriarContexto())
        {
            var versao = Convert.ToBase64String(await db.FechamentosSemanais.Where(f => f.ProfissionalId == prof)
                .Select(f => EF.Property<byte[]>(f, "Versao")).SingleAsync(ct));
            var usuario = new UsuarioAtualTeste(coord, Perfis.CoordenadorNap);
            var relogio = new RelogioTeste(Agora);
            await new ReabrirSemanaHandler(new ReabrirSemanaValidator(), new FechamentoRepository(db), usuario, relogio,
                AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo())
                .Handle(new ReabrirSemanaCommand(prof, Semana, "Corrigir lançamento", versao), ct);
        }
        await CorrigirAsync();
    }

    [Fact]
    public async Task Listar_semanas_traz_pendentes_fechamento_e_eventos_com_nome_do_autor()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional do fechamento");
        await using (var db = fx.CriarContexto())
        {
            db.Add(Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Sem desfecho", IntervaloHorario.Criar(Terca(8), Terca(9)),
                prof, null, null, prof));
            await db.SaveChangesAsync(ct);
        }

        await using (var db = fx.CriarContexto())
        {
            var ex = await Assert.ThrowsAsync<DomainException>(() => FecharAsync(db, prof, false));
            Assert.Equal("A semana tem 1 pendências de classificação.", ex.Message);
        }
        await using (var db = fx.CriarContexto()) await FecharAsync(db, prof, true);

        await using var leitura = fx.CriarContexto();
        var linhas = await new ProducaoQueries(leitura, new RelogioTeste(Agora))
            .ListarSemanasAsync([new(prof, "Profissional do fechamento")], new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 22), ct);

        Assert.Equal([Semana, Semana.AddDays(7)], linhas.Select(l => l.Semana));
        var fechada = linhas[0];
        Assert.True(fechada.Fechada);
        Assert.Equal(1, fechada.Pendentes);
        Assert.NotNull(fechada.Versao);
        var evento = Assert.Single(fechada.Eventos);
        Assert.Equal((TipoEventoFechamento.Fechamento, "Profissional do fechamento"), (evento.Tipo, evento.Autor));
        Assert.False(linhas[1].Fechada);
        Assert.Null(linhas[1].Versao);
        Assert.Empty(linhas[1].Eventos);
    }

    [Fact]
    public async Task Fechar_espera_a_escrita_em_andamento_na_semana_terminar()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var escritaTemTrava = new TaskCompletionSource();
        var liberarEscrita = new TaskCompletionSource();

        await using var dbEscrita = fx.CriarContexto();
        var relogio = new RelogioTeste(Agora);
        var escrita = new UnidadeDeTrabalho(dbEscrita).EmTransacaoAsync(async tct =>
        {
            // Mesma guarda usada pelos handlers de escrita: trava compartilhada até o commit.
            await new ServicoFechamento(new FechamentoRepository(dbEscrita), relogio).ExigirSemanaAbertaAsync(prof, Semana, tct);
            escritaTemTrava.SetResult();
            await liberarEscrita.Task;
            return 0;
        }, ct);
        await escritaTemTrava.Task;

        await using var dbFechar = fx.CriarContexto();
        var fechar = FecharAsync(dbFechar, prof, false);
        await Task.Delay(1500, ct);
        Assert.False(fechar.IsCompleted);

        liberarEscrita.SetResult();
        await escrita;
        await fechar;
        await using var leitura = fx.CriarContexto();
        Assert.True((await new FechamentoRepository(leitura).ObterAsync(prof, Semana, ct))!.Fechada);
    }
}
