using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Domain.Agenda;
using Nap.Domain.Alunos;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum;
using Nap.Domain.Comunicacoes;
using Nap.Domain.Documentos;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Encaminhamentos;
using Nap.Domain.Peis;
using Nap.Application.Features.Peis;
using Nap.Application.Features.Peis.Commands;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Consultas;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class RoundTripTests(SqlServerFixture fx)
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(-4));

    internal static async Task<Aluno> CriarAlunoAsync(SqlServerFixture fx, Guid cursoId, Guid profissionalId, string? nome = null)
    {
        var aluno = Aluno.Criar(nome ?? "Aluno de teste", Matricula.Criar(Guid.NewGuid().ToString("N")[..20]), cursoId, 3,
            new DateOnly(2026, 3, 1), profissionalId, "Necessidades", "Observações", profissionalId, Agora);
        await using var db = fx.CriarContexto();
        db.Alunos.Add(aluno);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return aluno;
    }

    [Fact]
    public async Task Aluno_round_trip_preserva_historico_e_vo()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var cursoA = await fx.CriarCursoAsync();
        var cursoB = await fx.CriarCursoAsync();
        var aluno = await CriarAlunoAsync(fx, cursoA, prof);

        await using (var a = fx.CriarContexto())
        {
            var carregado = (await new AlunoRepository(a).ObterAsync(aluno.Id, ct))!;
            carregado.AlterarSituacao(SituacaoAluno.Ativo, "Motivo da mudança", prof, Agora.AddMinutes(1));
            carregado.TransferirCurso(cursoB, new DateOnly(2026, 8, 1), "Transferência", prof, Agora.AddMinutes(2));
            await new UnidadeDeTrabalho(a).SalvarAsync(ct);
        }

        await using var b = fx.CriarContexto();
        var lido = (await new AlunoRepository(b).ObterAsync(aluno.Id, ct))!;
        Assert.Equal(aluno.Matricula, lido.Matricula);
        Assert.Equal(SituacaoAluno.Ativo, lido.Situacao);
        Assert.Equal(cursoB, lido.CursoAtualId);
        Assert.Equal([null, SituacaoAluno.EmAcompanhamento], lido.HistoricoSituacao.OrderBy(h => h.Em).ThenBy(h => h.Id).Select(h => h.De));
        Assert.Equal("Motivo da mudança", lido.HistoricoSituacao.Single(h => h.De is not null).Motivo);
        var cursos = lido.HistoricoCursos.OrderBy(v => v.Desde).ToList();
        Assert.Equal([cursoA, cursoB], cursos.Select(v => v.CursoId));
        Assert.Equal(new DateOnly(2026, 8, 1), cursos[0].Ate);
        Assert.Null(cursos[1].Ate);
        Assert.Equal(("Cadastro inicial", prof, Agora), (cursos[0].Motivo, cursos[0].AutorId, cursos[0].RegistradoEm));
        Assert.Equal(("Transferência", prof, Agora.AddMinutes(2)), (cursos[1].Motivo, cursos[1].AutorId, cursos[1].RegistradoEm));
        Assert.Equal(TimeSpan.Zero, cursos[1].RegistradoEm.Offset);
        Assert.True(await new AlunoRepository(b).MatriculaExisteAsync(aluno.Matricula.Valor, ct));
        Assert.False(await new AlunoRepository(b).MatriculaExisteAsync("INEXISTENTE", ct));
    }

    [Fact]
    public async Task Atendimento_round_trip_preserva_dados_clinicos_e_correcoes()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var aluno = await CriarAlunoAsync(fx, await fx.CriarCursoAsync(), prof);
        var intervalo = IntervaloHorario.Criar(Agora.AddHours(-3), Agora.AddHours(-2));
        var dados = new DadosClinicos("Demanda", "Privado " + new string('x', 6000), "Encaminhar", "Próximos");
        var atendimento = Atendimento.RegistrarAvulso(aluno.Id, prof, intervalo, TipoAtendimento.Psicopedagogico,
            EstadoAtendimento.Realizado, dados, "Justificativa do avulso", prof, Agora);

        await using (var a = fx.CriarContexto())
        {
            new AtendimentoRepository(a).Adicionar(atendimento);
            await new UnidadeDeTrabalho(a).SalvarAsync(ct);
        }
        await using (var a2 = fx.CriarContexto())
        {
            var carregado = (await new AtendimentoRepository(a2).ObterAsync(atendimento.Id, ct))!;
            carregado.Corrigir(EstadoAtendimento.Ausente, TipoAtendimento.Acompanhamento,
                IntervaloHorario.Criar(Agora.AddHours(-5), Agora.AddHours(-4)), null, "Correção do registro", prof, false, Agora);
            await new UnidadeDeTrabalho(a2).SalvarAsync(ct);
        }

        await using var b = fx.CriarContexto();
        var lido = (await new AtendimentoRepository(b).ObterAsync(atendimento.Id, ct))!;
        Assert.Equal(EstadoAtendimento.Ausente, lido.Estado);
        Assert.Null(lido.Dados);
        var correcao = Assert.Single(lido.Correcoes);
        Assert.Equal(dados, correcao.DadosAnteriores);
        Assert.Equal(TipoAtendimento.Psicopedagogico, correcao.TipoAnterior);
        Assert.Equal(intervalo.Inicio, correcao.InicioAnterior);
        Assert.Equal(intervalo.Fim, correcao.FimAnterior);
        Assert.Equal(EstadoAtendimento.Realizado, correcao.EstadoAnterior);
        Assert.Equal("Correção do registro", correcao.Justificativa);
        Assert.Equal("Justificativa do avulso", lido.JustificativaAvulso);

        var detalhe = (await new AtendimentoQueries(b, SqlServerFixture.Relogio()).ObterAsync(atendimento.Id, ct))!;
        Assert.True(detalhe.Avulso);
        Assert.Null(detalhe.DadosClinicos);
        Assert.Equal("Correção do registro", Assert.Single(detalhe.Correcoes).Justificativa);
    }

    [Fact]
    public async Task Encaminhamento_round_trip_preserva_evolucoes_e_datas()
    {
        var ct = TestContext.Current.CancellationToken;
        var enc = await OutboxTests.NovoEncaminhamentoAsync(fx);
        var hoje = new DateOnly(2026, 10, 2);
        await using (var a = fx.CriarContexto())
        {
            a.Add(enc);
            await new UnidadeDeTrabalho(a).SalvarAsync(ct);
        }
        await using (var a2 = fx.CriarContexto())
        {
            var carregado = await a2.Set<Encaminhamento>().Include(e => e.Evolucoes).SingleAsync(e => e.Id == enc.Id, ct);
            carregado.RegistrarRetorno(AcaoRetorno.Acolhimento, new DateOnly(2026, 10, 1), "Acolhido", enc.SolicitanteId, hoje, Agora.AddMinutes(1));
            carregado.RegistrarRetorno(AcaoRetorno.InicioAtendimento, hoje, null, enc.SolicitanteId, hoje, Agora.AddMinutes(2));
            await new UnidadeDeTrabalho(a2).SalvarAsync(ct);
        }

        await using var b = fx.CriarContexto();
        var lido = await b.Set<Encaminhamento>().Include(e => e.Evolucoes).SingleAsync(e => e.Id == enc.Id, ct);
        Assert.Equal(EstadoEncaminhamento.EmAtendimento, lido.Estado);
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), hoje, (DateOnly?)null),
            (lido.DataEncaminhamento, lido.DataAcolhimento, lido.DataPrimeiroAtendimento, lido.DataEncerramento));
        Assert.Equal(("Motivo restrito", "Resumo compartilhável", (string?)null), (lido.MotivoRestrito, lido.ResumoCompartilhavel, lido.Observacoes));
        Assert.Equal(TimeSpan.Zero, lido.CriadoEm.Offset);
        Assert.Equal((Agora, Agora.AddMinutes(2)), (lido.CriadoEm, lido.AtualizadoEm));
        var evolucoes = lido.Evolucoes.OrderBy(e => e.Em).ToList();
        Assert.Equal([AcaoRetorno.Acolhimento, AcaoRetorno.InicioAtendimento], evolucoes.Select(e => e.Acao));
        Assert.Equal((EstadoEncaminhamento.Encaminhado, EstadoEncaminhamento.Acolhido, new DateOnly(2026, 10, 1), "Acolhido"),
            (evolucoes[0].EstadoAnterior, evolucoes[0].EstadoNovo, evolucoes[0].Data, evolucoes[0].Texto));
        Assert.Equal((hoje, (string?)null, enc.SolicitanteId), (evolucoes[1].Data, evolucoes[1].Texto, evolucoes[1].AutorId));
        // Cada retorno virou uma linha na outbox junto com a evolução.
        Assert.Equal(3, await b.Outbox.CountAsync(m => m.Conteudo.Contains(enc.Id.ToString()), ct));
    }

    [Fact]
    public async Task Datetimeoffset_volta_em_utc()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var aluno = await CriarAlunoAsync(fx, await fx.CriarCursoAsync(), prof);
        var c = Compromisso.Criar(TipoCompromisso.Atendimento, "Atendimento", IntervaloHorario.Criar(Agora, Agora.AddHours(1)),
            prof, aluno.Id, null, prof, Agora);
        await using (var a = fx.CriarContexto())
        {
            new AgendaRepository(a).Adicionar(c);
            await new UnidadeDeTrabalho(a).SalvarAsync(ct);
        }

        await using var b = fx.CriarContexto();
        var lido = (await new AgendaRepository(b).ObterAsync(c.Id, ct))!;
        Assert.Equal(TimeSpan.Zero, lido.Intervalo.Inicio.Offset);
        Assert.Equal(Agora, lido.Intervalo.Inicio);
        // Instantes que vêm do relógio com deslocamento também são gravados em UTC.
        var mudanca = (await new AlunoRepository(b).ObterAsync(aluno.Id, ct))!.HistoricoSituacao.Single();
        Assert.Equal(TimeSpan.Zero, mudanca.Em.Offset);
        Assert.Equal(Agora, mudanca.Em);
    }

    [Fact]
    public async Task Atualizacao_concorrente_de_aluno_gera_conflito()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var aluno = await CriarAlunoAsync(fx, await fx.CriarCursoAsync(), prof);
        await using var a = fx.CriarContexto();
        await using var b = fx.CriarContexto();
        var noA = (await new AlunoRepository(a).ObterAsync(aluno.Id, ct))!;
        var noB = (await new AlunoRepository(b).ObterAsync(aluno.Id, ct))!;

        noA.AtualizarDados("Nome alterado em A", 4, null, null, prof);
        await new UnidadeDeTrabalho(a).SalvarAsync(ct);
        noB.AtualizarDados("Nome alterado em B", 5, null, null, prof);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => new UnidadeDeTrabalho(b).SalvarAsync(ct));
        Assert.Equal("O registro foi alterado por outra pessoa. Recarregue e tente novamente", ex.Message);
    }

    [Fact]
    public async Task Transacao_desfeita_deixa_o_contexto_limpo()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        await using var db = fx.CriarContexto();
        var uow = new UnidadeDeTrabalho(db);
        var aluno = Aluno.Criar("Aluno desfeito", Matricula.Criar(Guid.NewGuid().ToString("N")[..20]), curso, 1,
            new DateOnly(2026, 3, 1), prof, null, null, prof, Agora);

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.EmTransacaoAsync<Guid>(async tct =>
        {
            new AlunoRepository(db).Adicionar(aluno);
            AuditoriaTeste.Criar(db, new UsuarioAtualTeste(prof), SqlServerFixture.Relogio()).Registrar("aluno.criar", "Aluno", aluno.Id);
            await uow.SalvarAsync(tct);
            throw new InvalidOperationException("falha depois de salvar");
        }, ct));

        Assert.Empty(db.ChangeTracker.Entries());
        await uow.SalvarAsync(ct);
        await using var leitura = fx.CriarContexto();
        Assert.False(await leitura.Alunos.AnyAsync(a => a.Id == aluno.Id, ct));
        Assert.False(await leitura.EventosAuditoria.AnyAsync(e => e.RecursoId == aluno.Id, ct));
    }

    [Fact]
    public async Task Chave_de_idempotencia_reserva_uma_vez_e_devolve_o_recurso_concluido()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = Guid.NewGuid();
        var recurso = Guid.NewGuid();
        await using var db = fx.CriarContexto();
        var chaves = new ChavesIdempotencia(db, SqlServerFixture.Relogio());

        Assert.Equal(ResultadoReserva.Reservada, (await chaves.ReservarAsync(usuario, "chave-1", "POST /alunos", ct)).Resultado);
        Assert.Equal(new ReservaIdempotencia(ResultadoReserva.EmAndamento, null), await chaves.ReservarAsync(usuario, "chave-1", "POST /alunos", ct));
        await chaves.ConcluirAsync(usuario, "chave-1", recurso, ct);
        Assert.Equal(new ReservaIdempotencia(ResultadoReserva.Concluida, recurso), await chaves.ReservarAsync(usuario, "chave-1", "POST /alunos", ct));

        await chaves.LiberarAsync(usuario, "chave-1", ct);
        Assert.Equal(ResultadoReserva.Concluida, (await chaves.ReservarAsync(usuario, "chave-1", "POST /alunos", ct)).Resultado);
        await chaves.ReservarAsync(usuario, "chave-2", "POST /alunos", ct);
        await chaves.LiberarAsync(usuario, "chave-2", ct);
        Assert.Equal(ResultadoReserva.Reservada, (await chaves.ReservarAsync(usuario, "chave-2", "POST /alunos", ct)).Resultado);
        await Assert.ThrowsAsync<ConflictException>(() => chaves.ReservarAsync(usuario, "chave-2", "POST /agenda", ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => chaves.ConcluirAsync(usuario, "inexistente", recurso, ct));
    }

    [Fact]
    public async Task Reserva_em_andamento_abandonada_ha_mais_de_5_minutos_pode_ser_retomada()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = Guid.NewGuid();
        var relogio = new RelogioTeste(DateTimeOffset.UtcNow.AddMinutes(-10));
        await using var db = fx.CriarContexto();
        var chaves = new ChavesIdempotencia(db, relogio);

        await chaves.ReservarAsync(usuario, "chave-orfa", "POST /alunos", ct); // o processo "caiu" sem concluir nem liberar
        relogio.Agora = relogio.Agora.AddMinutes(4);
        Assert.Equal(ResultadoReserva.EmAndamento, (await chaves.ReservarAsync(usuario, "chave-orfa", "POST /alunos", ct)).Resultado);
        relogio.Agora = relogio.Agora.AddMinutes(6);
        await Assert.ThrowsAsync<ConflictException>(() => chaves.ReservarAsync(usuario, "chave-orfa", "POST /agenda", ct));

        Assert.Equal(ResultadoReserva.Reservada, (await chaves.ReservarAsync(usuario, "chave-orfa", "POST /alunos", ct)).Resultado);
        // Retomada renova CriadaEm: a próxima tentativa volta a ver a reserva em andamento.
        Assert.Equal(ResultadoReserva.EmAndamento, (await chaves.ReservarAsync(usuario, "chave-orfa", "POST /alunos", ct)).Resultado);
    }

    [Fact]
    public async Task Mensagem_round_trip_preserva_fio_vinculos_e_arquivamento()
    {
        var ct = TestContext.Current.CancellationToken;
        var autor = await fx.CriarProfissionalAsync();
        var destinatario = await fx.CriarProfissionalAsync(Perfis.CoordenadorCurso);
        var enc = await OutboxTests.NovoEncaminhamentoAsync(fx);
        var corpo = new string('c', 10000);
        var original = Mensagem.CriarRascunho(autor, TipoMensagem.RetornoEncaminhamento, "Assunto da conversa", corpo,
            destinatario, enc.CursoId, enc.AlunoId, enc.Id, Agora);
        original.Enviar(Agora.AddMinutes(1));
        await using (var a = fx.CriarContexto())
        {
            a.AddRange(enc, original);
            await new UnidadeDeTrabalho(a).SalvarAsync(ct);
        }
        Mensagem resposta;
        await using (var a2 = fx.CriarContexto())
        {
            var carregada = await a2.Set<Mensagem>().SingleAsync(m => m.Id == original.Id, ct);
            carregada.MarcarLida(destinatario, Agora.AddMinutes(2));
            carregada.Arquivar(autor, Agora.AddMinutes(3));
            resposta = Mensagem.CriarResposta(carregada, destinatario, "Resposta", Agora.AddMinutes(4));
            a2.Add(resposta);
            await new UnidadeDeTrabalho(a2).SalvarAsync(ct);
        }

        await using var b = fx.CriarContexto();
        var lida = await b.Set<Mensagem>().SingleAsync(m => m.Id == original.Id, ct);
        Assert.Equal((autor, (Guid?)destinatario, TipoMensagem.RetornoEncaminhamento, EstadoMensagem.Enviada),
            (lida.AutorId, lida.DestinatarioId, lida.Tipo, lida.Estado));
        Assert.Equal(("Assunto da conversa", corpo), (lida.Assunto, lida.Corpo));
        Assert.Equal(((Guid?)enc.CursoId, (Guid?)enc.AlunoId, (Guid?)enc.Id), (lida.CursoId, lida.AlunoId, lida.EncaminhamentoId));
        Assert.Equal((original.Id, (Guid?)null), (lida.FioId, lida.RespostaDeId));
        Assert.Equal((Agora, Agora.AddMinutes(1), (DateTimeOffset?)Agora.AddMinutes(1), (DateTimeOffset?)Agora.AddMinutes(2)),
            (lida.CriadaEm, lida.AtualizadaEm, lida.EnviadaEm, lida.LidaEm));
        Assert.Equal(((DateTimeOffset?)Agora.AddMinutes(3), (DateTimeOffset?)null), (lida.ArquivadaPeloAutorEm, lida.ArquivadaPeloDestinatarioEm));
        Assert.Equal(TimeSpan.Zero, lida.CriadaEm.Offset);
        var lidaResposta = await b.Set<Mensagem>().SingleAsync(m => m.Id == resposta.Id, ct);
        Assert.Equal((original.Id, (Guid?)original.Id, (Guid?)autor, "Re: Assunto da conversa"),
            (lidaResposta.FioId, lidaResposta.RespostaDeId, lidaResposta.DestinatarioId, lidaResposta.Assunto));
        // Cada envio virou uma linha na outbox.
        Assert.Equal(2, await b.Outbox.CountAsync(m => m.Tipo == "MensagemEnviada"
            && (m.Conteudo.Contains(original.Id.ToString()) || m.Conteudo.Contains(resposta.Id.ToString())), ct));
    }

    [Fact]
    public async Task Documento_round_trip_preserva_metadados_e_compartilhamento()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = await CriarAlunoAsync(fx, curso, prof);
        var chave = Guid.CreateVersion7().ToString("N");
        var sha = new string('a', 64);
        var doc = Documento.Criar(aluno.Id, curso, CategoriaDocumento.Laudo, "Laudo inicial", "C:\\pasta\\laudo.pdf",
            "application/pdf", 1234, sha, chave, prof, Agora);
        await using (var a = fx.CriarContexto())
        {
            a.Add(doc);
            await new UnidadeDeTrabalho(a).SalvarAsync(ct);
        }
        await using (var a2 = fx.CriarContexto())
        {
            var carregado = await a2.Set<Documento>().SingleAsync(d => d.Id == doc.Id, ct);
            carregado.Compartilhar("Necessário para a coordenação", prof, Agora.AddMinutes(1));
            carregado.Arquivar("Documento substituído", prof, Agora.AddMinutes(2));
            await new UnidadeDeTrabalho(a2).SalvarAsync(ct);
        }

        await using var b = fx.CriarContexto();
        var lido = await b.Set<Documento>().SingleAsync(d => d.Id == doc.Id, ct);
        Assert.Equal((aluno.Id, curso, CategoriaDocumento.Laudo, "Laudo inicial"), (lido.AlunoId, lido.CursoId, lido.Categoria, lido.Descricao));
        Assert.Equal(("laudo.pdf", "application/pdf", 1234L, sha, chave),
            (lido.NomeArquivo, lido.TipoConteudo, lido.TamanhoBytes, lido.Sha256, lido.ChaveArmazenamento));
        Assert.Equal(VisibilidadeDocumento.CompartilhadoCoordenacao, lido.Visibilidade);
        Assert.Equal(((DateTimeOffset?)Agora.AddMinutes(1), (Guid?)prof, "Necessário para a coordenação"),
            (lido.CompartilhadoEm, lido.CompartilhadoPorId, lido.CompartilhadoJustificativa));
        Assert.True(lido.Arquivado);
        Assert.Equal(((DateTimeOffset?)Agora.AddMinutes(2), (Guid?)prof, "Documento substituído"),
            (lido.ArquivadoEm, lido.ArquivadoPorId, lido.ArquivadoMotivo));
        Assert.Equal((prof, Agora, Agora.AddMinutes(2)), (lido.CriadoPorId, lido.CriadoEm, lido.AtualizadoEm));

        // A chave de armazenamento é única: dois registros nunca apontam para o mesmo arquivo.
        await using var c = fx.CriarContexto();
        c.Add(Documento.Criar(aluno.Id, curso, CategoriaDocumento.Outro, "Outro documento", "x.pdf", "application/pdf", 1, sha,
            chave, prof, Agora));
        await Assert.ThrowsAsync<DbUpdateException>(() => c.SaveChangesAsync(ct));
    }

    private static ConteudoPei ConteudoDePei() => new("Demanda interna", "Necessidades", null, "Orientações", null, "Cuidados",
        PeriodoVigencia.Criar(new DateOnly(2026, 8, 1), new DateOnly(2027, 7, 31)));

    /// <summary>PEI com v1 publicada (duas assinaturas e uma ciência) e v2 em rascunho, já gravado.</summary>
    private async Task<(Pei pei, Guid prof, Guid coord, Guid curso)> NovoPeiPublicadoAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var coord = await fx.CriarProfissionalAsync(Perfis.CoordenadorNap);
        var curso = await fx.CriarCursoAsync();
        var aluno = await CriarAlunoAsync(fx, curso, prof);
        var pei = Pei.Criar(aluno.Id, prof, ConteudoDePei(), prof, Agora);
        var v1 = pei.Versoes.Single();
        v1.EnviarParaRevisao(Agora.AddMinutes(1));
        var hash = CalculadoraHashPei.Calcular(v1.Conteudo);
        v1.AprovarRevisao(coord, hash, Agora.AddMinutes(2));
        v1.RegistrarAssinatura(PapelAssinatura.Profissional, prof, "Profissional", hash, "evidencia-1", Agora.AddMinutes(3));
        v1.RegistrarAssinatura(PapelAssinatura.Aluno, null, "Aluno", hash, "evidencia-2", Agora.AddMinutes(4));
        pei.Publicar(v1, coord, curso, Agora.AddMinutes(5));
        v1.RegistrarCiencia(coord, Agora.AddMinutes(6));
        pei.NovaVersao(prof, Agora.AddMinutes(7));
        await using var db = fx.CriarContexto();
        db.Add(pei);
        await db.SaveChangesAsync(ct);
        return (pei, prof, coord, curso);
    }

    private static IQueryable<Pei> PeiCompleto(NapDbContext db) =>
        db.Peis.Include(p => p.Versoes).ThenInclude(v => v.Assinaturas).Include(p => p.Versoes).ThenInclude(v => v.Ciencias);

    [Fact]
    public async Task Pei_round_trip_preserva_versoes_assinaturas_e_ciencia_em_dois_contextos()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pei, prof, coord, curso) = await NovoPeiPublicadoAsync();

        await using var b = fx.CriarContexto();
        var lido = await PeiCompleto(b).SingleAsync(p => p.Id == pei.Id, ct);
        Assert.Equal((pei.AlunoId, prof, Agora), (lido.AlunoId, lido.ProfissionalResponsavelId, lido.CriadoEm));
        var v = lido.Versoes.OrderBy(x => x.Numero).ToList();
        Assert.Equal([1, 2], v.Select(x => x.Numero));
        Assert.Equal([EstadoVersaoPei.Publicada, EstadoVersaoPei.Rascunho], v.Select(x => x.Estado));
        var esperado = ConteudoDePei();
        Assert.Equal(esperado, v[0].Conteudo);
        Assert.Equal(esperado, v[1].Conteudo);
        Assert.Null(v[0].Conteudo.Adaptacoes);
        Assert.Equal(new DateOnly(2027, 7, 31), v[0].Conteudo.Vigencia.Fim);
        Assert.Equal(CalculadoraHashPei.Calcular(esperado), v[0].HashConteudo);
        Assert.Equal((coord, (Guid?)curso, (DateTimeOffset?)Agora.AddMinutes(5)), (v[0].PublicadaPorId, v[0].CursoPublicacaoId, v[0].PublicadaEm));
        Assert.Null(v[1].HashConteudo);
        Assert.Equal([PapelAssinatura.Profissional, PapelAssinatura.Aluno], v[0].Assinaturas.OrderBy(a => a.Papel).Select(a => a.Papel));
        var assinaturaProf = v[0].Assinaturas.Single(a => a.Papel == PapelAssinatura.Profissional);
        Assert.Equal(("Demonstrativa", "evidencia-1", (Guid?)prof), (assinaturaProf.Metodo, assinaturaProf.Evidencia, assinaturaProf.SignatarioId));
        Assert.Null(v[0].Assinaturas.Single(a => a.Papel == PapelAssinatura.Aluno).SignatarioId);
        Assert.Equal(coord, v[0].Ciencias.Single().CoordenadorId);
        Assert.Empty(v[1].Assinaturas);

        // Segundo contexto: a v2 é publicada e a v1 arquivada no mesmo contexto carregado.
        var v2 = v[1];
        v2.EnviarParaRevisao(Agora.AddMinutes(10));
        var hash = CalculadoraHashPei.Calcular(v2.Conteudo);
        v2.AprovarRevisao(coord, hash, Agora.AddMinutes(11));
        v2.RegistrarAssinatura(PapelAssinatura.Profissional, prof, "Profissional", hash, "e3", Agora.AddMinutes(12));
        v2.RegistrarAssinatura(PapelAssinatura.Aluno, null, "Aluno", hash, "e4", Agora.AddMinutes(13));
        lido.Publicar(v2, coord, curso, Agora.AddMinutes(14));
        await b.SaveChangesAsync(ct);

        await using var c = fx.CriarContexto();
        var final = await PeiCompleto(c).SingleAsync(p => p.Id == pei.Id, ct);
        Assert.Equal([EstadoVersaoPei.Arquivada, EstadoVersaoPei.Publicada],
            final.Versoes.OrderBy(x => x.Numero).Select(x => x.Estado));
        Assert.Equal("Substituída pela versão 2", final.Versoes.Single(x => x.Numero == 1).MotivoArquivamento);
    }

    [Fact]
    public async Task Troca_de_responsavel_do_pei_round_trip_preserva_o_historico()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pei, prof, coord, _) = await NovoPeiPublicadoAsync();
        var novo = await fx.CriarProfissionalAsync();

        await using (var a = fx.CriarContexto())
        {
            var carregado = await new PeiRepository(a).ObterAsync(pei.Id, ct);
            carregado!.TrocarResponsavel(novo, "Licença da responsável", coord, Agora.AddDays(1));
            await a.SaveChangesAsync(ct);
        }
        await using (var b = fx.CriarContexto())
        {
            var lido = await new PeiRepository(b).ObterAsync(pei.Id, ct);
            lido!.TrocarResponsavel(prof, "Retorno da licença", coord, Agora.AddDays(2));
            await b.SaveChangesAsync(ct);
        }

        await using var c = fx.CriarContexto();
        var final = await c.Peis.SingleAsync(p => p.Id == pei.Id, ct);
        Assert.Equal(prof, final.ProfissionalResponsavelId);
        Assert.Equal([new TrocaResponsavelPei(prof, novo, "Licença da responsável", coord, Agora.AddDays(1)),
                      new TrocaResponsavelPei(novo, prof, "Retorno da licença", coord, Agora.AddDays(2))],
            final.TrocasResponsavel.OrderBy(t => t.Em));
    }

    [Fact]
    public async Task Ciencias_simultaneas_do_mesmo_coordenador_gravam_uma_so_sem_conflito()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pei, _, _, curso) = await NovoPeiPublicadoAsync();
        var coordCurso = await fx.CriarProfissionalAsync(Perfis.CoordenadorCurso);
        var v1 = pei.Versoes.Single(v => v.Numero == 1).Id;

        Task Ciencia(NapDbContext db, IPeiRepository peis)
        {
            var usuario = new UsuarioAtualTeste(coordCurso, Perfis.CoordenadorCurso, curso);
            var relogio = new RelogioTeste(Agora.AddHours(1));
            return new RegistrarCienciaPeiHandler(new RegistrarCienciaPeiValidator(), peis, usuario, relogio,
                AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db), new CacheNulo()).Handle(new RegistrarCienciaPeiCommand(v1), ct);
        }

        // c leu o PEI antes de b gravar (e não relê): a segunda gravação esbarra no índice único e continua sendo sucesso.
        await using var b = fx.CriarContexto();
        await using var c = fx.CriarContexto();
        var lidoAntes = (await new PeiRepository(c).ObterPorVersaoAsync(v1, ct))!;
        await Ciencia(b, new PeiRepository(b));
        await Ciencia(c, new PeiJaLido(lidoAntes));

        await using var d = fx.CriarContexto();
        Assert.Equal(1, await d.Set<CienciaPei>().CountAsync(x => EF.Property<Guid>(x, "VersaoId") == v1 && x.CoordenadorId == coordCurso, ct));
    }

    /// <summary>Devolve o PEI lido antes, sem nova consulta: fixa o momento da leitura numa corrida.</summary>
    private sealed class PeiJaLido(Pei pei) : IPeiRepository
    {
        public Task<Pei?> ObterAsync(Guid peiId, CancellationToken ct) => Task.FromResult<Pei?>(pei);
        public Task<Pei?> ObterPorVersaoAsync(Guid versaoId, CancellationToken ct) => Task.FromResult<Pei?>(pei);
        public void Adicionar(Pei p) => throw new NotSupportedException();
        public void RemoverVersao(VersaoPei v) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Segunda_versao_aberta_viola_indice()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pei, _, _, _) = await NovoPeiPublicadoAsync();

        await using var b = fx.CriarContexto();
        var lido = await PeiCompleto(b).SingleAsync(p => p.Id == pei.Id, ct);
        // A v2 já é rascunho; reabrir a v1 deixaria duas versões abertas.
        b.Entry(lido.Versoes.Single(v => v.Numero == 1)).Property(v => v.Estado).CurrentValue = EstadoVersaoPei.Rascunho;
        await Assert.ThrowsAsync<DbUpdateException>(() => b.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Repositorio_descarta_e_cria_versao_e_traduz_versao_aberta_duplicada()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pei, prof, _, _) = await NovoPeiPublicadoAsync();

        await using (var a = fx.CriarContexto())
        {
            var repo = new PeiRepository(a);
            var lido = (await repo.ObterAsync(pei.Id, ct))!;
            var v2 = lido.Versoes.Single(v => v.Numero == 2);
            lido.DescartarVersao(v2);
            repo.RemoverVersao(v2);
            await new UnidadeDeTrabalho(a).SalvarAsync(ct);
        }

        // Duas novas versões simultâneas: a segunda gravação vira conflito com a mensagem de versão aberta.
        await using var b = fx.CriarContexto();
        await using var c = fx.CriarContexto();
        var emB = (await new PeiRepository(b).ObterPorVersaoAsync(pei.Versoes.Single(v => v.Numero == 1).Id, ct))!;
        var emC = (await new PeiRepository(c).ObterAsync(pei.Id, ct))!;
        Assert.Single(emB.Versoes);
        Assert.Equal(2, emB.Versoes.Single().Assinaturas.Count);
        var nova = emB.NovaVersao(prof, Agora.AddHours(1));
        emC.NovaVersao(prof, Agora.AddHours(1));
        await new UnidadeDeTrabalho(b).SalvarAsync(ct);
        var erro = await Assert.ThrowsAsync<ConflictException>(() => new UnidadeDeTrabalho(c).SalvarAsync(ct));
        Assert.Equal("Já existe uma versão em elaboração.", erro.Message);

        await using var d = fx.CriarContexto();
        var final = (await new PeiRepository(d).ObterAsync(pei.Id, ct))!;
        Assert.Equal([1, 2], final.Versoes.OrderBy(v => v.Numero).Select(v => v.Numero));
        Assert.Equal(nova.Id, final.VersaoAberta!.Id);
    }

    [Fact]
    public async Task Assinatura_duplicada_por_papel_viola_unicidade()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pei, _, _, _) = await NovoPeiPublicadoAsync();

        await using var b = fx.CriarContexto();
        var lido = await PeiCompleto(b).SingleAsync(p => p.Id == pei.Id, ct);
        var doAluno = lido.Versoes.Single(v => v.Numero == 1).Assinaturas.Single(a => a.Papel == PapelAssinatura.Aluno);
        b.Entry(doAluno).Property(a => a.Papel).CurrentValue = PapelAssinatura.Profissional;
        await Assert.ThrowsAsync<DbUpdateException>(() => b.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Mensagem_envio_pei_round_trip_com_versao()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pei, _, coord, curso) = await NovoPeiPublicadoAsync();
        var destinatario = await fx.CriarProfissionalAsync(Perfis.CoordenadorCurso);
        var versaoId = pei.VersaoPublicada!.Id;
        var msg = Mensagem.CriarEnvioPei(coord, destinatario, "PEI publicado: Aluno (versão 1)", "Corpo fixo", curso,
            pei.AlunoId, versaoId, Agora.AddMinutes(5));
        await using (var a = fx.CriarContexto())
        {
            a.Add(msg);
            await new UnidadeDeTrabalho(a).SalvarAsync(ct);
        }

        await using var b = fx.CriarContexto();
        var lida = await b.Set<Mensagem>().SingleAsync(m => m.Id == msg.Id, ct);
        Assert.Equal(((Guid?)versaoId, TipoMensagem.EnvioPei, EstadoMensagem.Enviada, (Guid?)destinatario),
            (lida.PeiVersaoId, lida.Tipo, lida.Estado, lida.DestinatarioId));
        Assert.Equal(1, await b.Outbox.CountAsync(m => m.Tipo == "MensagemEnviada" && m.Conteudo.Contains(msg.Id.ToString()), ct));
    }

    [Fact]
    public async Task Tentativas_erradas_concorrentes_do_acesso_nao_passam_as_duas()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pei, prof, _, _) = await NovoPeiPublicadoAsync();
        var publicada = pei.VersaoPublicada!;
        var (acesso, token, _) = AcessoAssinaturaPei.Gerar(publicada.Id, publicada.HashConteudo!, prof, Agora);
        await using (var a = fx.CriarContexto())
        {
            a.Add(acesso);
            await a.SaveChangesAsync(ct);
        }

        await using var c1 = fx.CriarContexto();
        await using var c2 = fx.CriarContexto();
        var a1 = await c1.AcessosAssinaturaPei.SingleAsync(x => x.Id == acesso.Id, ct);
        var a2 = await c2.AcessosAssinaturaPei.SingleAsync(x => x.Id == acesso.Id, ct);
        a1.VerificarCodigo(token, "x", Agora);
        a2.VerificarCodigo(token, "y", Agora);
        await c1.SaveChangesAsync(ct);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => c2.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Escala_com_faixas_round_trip()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var escala = EscalaProfissional.Criar(prof, new DateOnly(2026, 10, 1), null,
        [
            new FaixaEscala(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0), false),
            new FaixaEscala(DayOfWeek.Friday, new TimeOnly(7, 0), new TimeOnly(17, 0), true),
        ]);
        await using (var db = fx.CriarContexto())
        {
            db.EscalasProfissional.Add(escala);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var lida = await leitura.EscalasProfissional.SingleAsync(e => e.Id == escala.Id, ct);
        Assert.Equal((prof, new DateOnly(2026, 10, 1), (DateOnly?)null), (lida.ProfissionalId, lida.VigenciaInicio, lida.VigenciaFim));
        Assert.Equal(escala.Faixas.OrderBy(f => f.Dia), lida.Faixas.OrderBy(f => f.Dia));
    }

    [Fact]
    public async Task Feriado_round_trip()
    {
        var ct = TestContext.Current.CancellationToken;
        var feriado = Feriado.Criar(new DateOnly(2027, 9, 7), "Independência");
        await using (var db = fx.CriarContexto())
        {
            db.Feriados.Add(feriado);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var lido = await leitura.Feriados.SingleAsync(f => f.Id == feriado.Id, ct);
        Assert.Equal((new DateOnly(2027, 9, 7), "Independência", true), (lido.Data, lido.Descricao, lido.Ativo));
    }

    [Fact]
    public async Task Feriado_ativo_duplicado_na_mesma_data_viola_indice()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = new DateOnly(2027, 11, 15);
        await using var db = fx.CriarContexto();
        db.Feriados.Add(Feriado.Criar(data, "Proclamação da República"));
        await db.SaveChangesAsync(ct);

        await using var outro = fx.CriarContexto();
        outro.Feriados.Add(Feriado.Criar(data, "Duplicado"));
        await Assert.ThrowsAsync<DbUpdateException>(() => outro.SaveChangesAsync(ct));

        // Inativado, a data fica livre de novo.
        var primeiro = await db.Feriados.SingleAsync(f => f.Data == data && f.Ativo, ct);
        primeiro.Inativar();
        await db.SaveChangesAsync(ct);
        await using var terceiro = fx.CriarContexto();
        terceiro.Feriados.Add(Feriado.Criar(data, "Recadastrado"));
        await terceiro.SaveChangesAsync(ct);
    }
}
