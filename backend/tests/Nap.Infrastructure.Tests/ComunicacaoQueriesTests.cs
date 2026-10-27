using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Comunicacoes;
using Nap.Domain.Comum;
using Nap.Domain.Comunicacoes;
using Nap.Domain.Encaminhamentos;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Tests;

/// <summary>Cada teste cria usuários próprios: o recorte por participante isola os dados no banco compartilhado.</summary>
public class ComunicacaoQueriesTests(SqlServerFixture fx)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateTimeOffset Em(int minuto) => new(2026, 10, 2, 9, minuto, 0, TimeSpan.FromHours(-3));

    private Task<Guid> UsuarioAsync(string nome) => fx.CriarProfissionalAsync(Perfis.ProfissionalNap, nome);

    private static Mensagem Enviada(Guid autor, Guid destinatario, int minuto, string assunto = "Assunto", string corpo = "Corpo")
    {
        var m = Mensagem.CriarRascunho(autor, TipoMensagem.Aviso, assunto, corpo, destinatario, null, null, null, Em(minuto));
        m.Enviar(Em(minuto));
        return m;
    }

    /// <summary>Sem eventos: estes testes não alimentam a outbox compartilhada.</summary>
    private async Task SalvarAsync(params Mensagem[] mensagens)
    {
        foreach (var m in mensagens) m.LimparEventos();
        await using var db = fx.CriarContexto();
        db.Mensagens.AddRange(mensagens);
        await db.SaveChangesAsync(Ct);
    }

    private async Task<IReadOnlyList<MensagemListaDto>> CaixaAsync(Guid usuario, CaixaMensagens caixa, string? busca = null)
    {
        await using var db = fx.CriarContexto();
        return (await new ComunicacaoQueries(db).ListarAsync(new UsuarioAtualTeste(usuario), caixa, busca, null, null, 1, 100, Ct)).Itens;
    }

    private async Task<MensagemDetalheDto?> DetalheAsync(Guid id, Guid usuario)
    {
        await using var db = fx.CriarContexto();
        return await new ComunicacaoQueries(db).ObterAsync(id, new UsuarioAtualTeste(usuario), Ct);
    }

    [Fact]
    public async Task Coordenador_so_le_enquanto_o_aluno_esta_no_curso_dele()
    {
        var prof = await UsuarioAsync("Ana");
        var curso = await fx.CriarCursoAsync();
        var outroCurso = await fx.CriarCursoAsync();
        var coordenador = await fx.CriarProfissionalAsync(Perfis.CoordenadorCurso, "Coordenação");
        var leitor = new UsuarioAtualTeste(coordenador, Perfis.CoordenadorCurso, curso);
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        var sobreAluno = Mensagem.CriarRascunho(prof, TipoMensagem.Aviso, "Aluno", "Corpo", coordenador, curso, aluno.Id, null, Em(1));
        sobreAluno.Enviar(Em(1));
        var semVinculo = Enviada(prof, coordenador, 2);
        await SalvarAsync(sobreAluno, semVinculo);

        await using (var db = fx.CriarContexto())
        {
            var consultas = new ComunicacaoQueries(db);
            Assert.Equal(2, (await consultas.ListarAsync(leitor, CaixaMensagens.Entrada, null, null, null, 1, 100, Ct)).Total);
            Assert.NotNull(await consultas.ObterAsync(sobreAluno.Id, leitor, Ct));
        }

        await using (var db = fx.CriarContexto())
        {
            var transferido = (await new Persistencia.Repositorios.AlunoRepository(db).ObterAsync(aluno.Id, Ct))!;
            transferido.TransferirCurso(outroCurso, new DateOnly(2026, 10, 1), "Transferência", prof, Em(3));
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = fx.CriarContexto())
        {
            var consultas = new ComunicacaoQueries(db);
            var entrada = await consultas.ListarAsync(leitor, CaixaMensagens.Entrada, null, null, null, 1, 100, Ct);
            Assert.Equal([semVinculo.Id], entrada.Itens.Select(m => m.Id));
            Assert.Null(await consultas.ObterAsync(sobreAluno.Id, leitor, Ct));
            Assert.Equal(1, await consultas.ContarNaoLidasAsync(leitor, Ct));
            // A Equipe NAP segue lendo.
            Assert.NotNull(await consultas.ObterAsync(sobreAluno.Id, new UsuarioAtualTeste(prof), Ct));
        }
    }

    [Fact]
    public async Task Sip_so_le_sem_vinculo_ou_com_encaminhamento_destinado_ao_sip()
    {
        var prof = await UsuarioAsync("Ana");
        var sip = await fx.CriarProfissionalAsync(Perfis.Sip, "SIP");
        var leitor = new UsuarioAtualTeste(sip, Perfis.Sip);
        var curso = await fx.CriarCursoAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        var hoje = new DateOnly(2026, 10, 2);
        var paraSip = Encaminhamento.Criar(aluno.Id, curso, prof, Especialidade.Psicologia, DestinoEncaminhamento.Sip, hoje, "motivo",
            "resumo", null, hoje, Em(0));
        var paraCoordenacao = Encaminhamento.Criar(aluno.Id, curso, prof, Especialidade.CoordenacaoCurso,
            DestinoEncaminhamento.CoordenacaoCurso, hoje, "motivo", "resumo", null, hoje, Em(0));
        paraSip.LimparEventos();
        paraCoordenacao.LimparEventos();
        await using (var db = fx.CriarContexto())
        {
            db.Encaminhamentos.AddRange(paraSip, paraCoordenacao);
            await db.SaveChangesAsync(Ct);
        }
        Mensagem Com(Guid? cursoId, Guid? alunoId, Guid? encaminhamentoId, int minuto)
        {
            var m = Mensagem.CriarRascunho(prof, TipoMensagem.Aviso, "Assunto", "Corpo", sip, cursoId, alunoId, encaminhamentoId, Em(minuto));
            m.Enviar(Em(minuto));
            return m;
        }
        var semVinculo = Com(null, null, null, 1);
        var comEncaminhamentoSip = Com(curso, aluno.Id, paraSip.Id, 2);
        var soAluno = Com(curso, aluno.Id, null, 3);
        var soCurso = Com(curso, null, null, 4);
        var outroEncaminhamento = Com(curso, aluno.Id, paraCoordenacao.Id, 5);
        await SalvarAsync(semVinculo, comEncaminhamentoSip, soAluno, soCurso, outroEncaminhamento);

        await using var leitura = fx.CriarContexto();
        var consultas = new ComunicacaoQueries(leitura);
        var entrada = await consultas.ListarAsync(leitor, CaixaMensagens.Entrada, null, null, null, 1, 100, Ct);
        Assert.Equal([comEncaminhamentoSip.Id, semVinculo.Id], entrada.Itens.Select(m => m.Id));
        Assert.Equal(2, await consultas.ContarNaoLidasAsync(leitor, Ct));
        foreach (var oculta in new[] { soAluno, soCurso, outroEncaminhamento })
            Assert.Null(await consultas.ObterAsync(oculta.Id, leitor, Ct));
        Assert.NotNull(await consultas.ObterAsync(comEncaminhamentoSip.Id, leitor, Ct));
    }

    [Fact]
    public async Task Entrada_e_enviados_so_do_usuario()
    {
        var a = await UsuarioAsync("Ana");
        var b = await UsuarioAsync("Bruno");
        var c = await UsuarioAsync("Carla");
        var aParaB = Enviada(a, b, 1);
        var bParaA = Enviada(b, a, 2);
        var cParaB = Enviada(c, b, 3);
        cParaB.MarcarLida(b, Em(4));
        var rascunho = Mensagem.CriarRascunho(a, TipoMensagem.Aviso, "Rascunho", "Texto", b, null, null, null, Em(5));
        await SalvarAsync(aParaB, bParaA, cParaB, rascunho);

        Assert.Equal([bParaA.Id], (await CaixaAsync(a, CaixaMensagens.Entrada)).Select(m => m.Id));
        Assert.Equal([aParaB.Id], (await CaixaAsync(a, CaixaMensagens.Enviados)).Select(m => m.Id));
        Assert.Equal([rascunho.Id], (await CaixaAsync(a, CaixaMensagens.Rascunhos)).Select(m => m.Id));
        Assert.Empty(await CaixaAsync(b, CaixaMensagens.Rascunhos));

        var entradaB = await CaixaAsync(b, CaixaMensagens.Entrada);
        Assert.Equal([cParaB.Id, aParaB.Id], entradaB.Select(m => m.Id));
        Assert.Equal(("Carla", "Bruno", true, true, Em(3)),
            (entradaB[0].Remetente, entradaB[0].Destinatario, entradaB[0].Lida, entradaB[0].Recebida, entradaB[0].Data));
        Assert.False(entradaB[1].Lida);
        Assert.All(await CaixaAsync(b, CaixaMensagens.Enviados), m => Assert.False(m.Recebida));
    }

    [Fact]
    public async Task Busca_no_recorte_do_usuario()
    {
        var a = await UsuarioAsync("Ana");
        var b = await UsuarioAsync("Bruno");
        var c = await UsuarioAsync("Carla");
        var d = await UsuarioAsync("Davi");
        var termo = $"termo{Guid.NewGuid():N}";
        var minha = Enviada(a, b, 1, corpo: $"Corpo com {termo} no meio");
        var alheia = Enviada(c, d, 2, assunto: $"Assunto {termo}");
        var porcento = Enviada(a, b, 3, assunto: "Meta de 100% atingida");
        var semPorcento = Enviada(a, b, 4, assunto: "Meta de 1000 atingida");
        var sublinhado = Enviada(a, b, 5, assunto: "Código a_b");
        var semSublinhado = Enviada(a, b, 6, assunto: "Código axb");
        var colchete = Enviada(a, b, 7, assunto: "Item [x]");
        await SalvarAsync(minha, alheia, porcento, semPorcento, sublinhado, semSublinhado, colchete);

        Assert.Equal([minha.Id], (await CaixaAsync(a, CaixaMensagens.Enviados, termo)).Select(m => m.Id));
        Assert.Equal([minha.Id], (await CaixaAsync(b, CaixaMensagens.Entrada, termo)).Select(m => m.Id));
        Assert.Empty(await CaixaAsync(a, CaixaMensagens.Entrada, termo));
        Assert.Equal([alheia.Id], (await CaixaAsync(d, CaixaMensagens.Entrada, termo)).Select(m => m.Id));
        // Curingas do LIKE são literais.
        Assert.Equal([porcento.Id], (await CaixaAsync(a, CaixaMensagens.Enviados, "100%")).Select(m => m.Id));
        Assert.Equal([sublinhado.Id], (await CaixaAsync(a, CaixaMensagens.Enviados, "a_b")).Select(m => m.Id));
        Assert.Equal([colchete.Id], (await CaixaAsync(a, CaixaMensagens.Enviados, "[x]")).Select(m => m.Id));
    }

    [Fact]
    public async Task Fio_mostra_so_mensagens_do_participante()
    {
        var a = await UsuarioAsync("Ana");
        var b = await UsuarioAsync("Bruno");
        var c = await UsuarioAsync("Carla");
        var original = Enviada(a, b, 1, assunto: "Pergunta", corpo: "Primeira");
        var resposta = Mensagem.CriarResposta(original, b, "Segunda", Em(2));
        var deTerceiros = Enviada(c, b, 3, corpo: "De outra conversa");
        var rascunhoDoDestinatario = Mensagem.CriarRascunho(b, TipoMensagem.Aviso, "Rascunho", "Texto", a, null, null, null, Em(4));
        await SalvarAsync(original, resposta, deTerceiros, rascunhoDoDestinatario);
        // Defensivo: mesmo uma mensagem alheia gravada no mesmo fio não aparece para quem não participa dela.
        await using (var db = fx.CriarContexto())
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE nap.Mensagens SET FioId = {original.FioId} WHERE Id = {deTerceiros.Id}", Ct);

        var detalhe = await DetalheAsync(original.Id, a);

        Assert.NotNull(detalhe);
        Assert.Equal([original.Id, resposta.Id], detalhe.Fio.Select(f => f.Id));
        Assert.Equal(["Primeira", "Segunda"], detalhe.Fio.Select(f => f.Corpo));
        Assert.Equal(("Pergunta", "Ana", "Bruno", false, true, false), (detalhe.Assunto, detalhe.Remetente, detalhe.Destinatario,
            detalhe.Recebida, detalhe.PodeResponder, detalhe.PodeMarcarLida));
        Assert.False(string.IsNullOrEmpty(detalhe.Versao));
        Assert.True((await DetalheAsync(original.Id, b))!.PodeMarcarLida);
        Assert.Null(await DetalheAsync(original.Id, c));
        Assert.Null(await DetalheAsync(rascunhoDoDestinatario.Id, a));
        Assert.Equal(EstadoMensagem.Rascunho, (await DetalheAsync(rascunhoDoDestinatario.Id, b))!.Estado);
    }

    [Fact]
    public async Task Nao_pode_responder_a_participante_inativo_ou_da_gestao()
    {
        var a = await UsuarioAsync("Ana");
        var b = await UsuarioAsync("Bruno");
        var gestao = await fx.CriarProfissionalAsync(Perfis.Gestao, "Gestão");
        var paraB = Enviada(a, b, 1);
        var paraGestao = Enviada(a, gestao, 2);
        await SalvarAsync(paraB, paraGestao);

        Assert.True((await DetalheAsync(paraB.Id, a))!.PodeResponder);
        Assert.False((await DetalheAsync(paraGestao.Id, a))!.PodeResponder);
        await using (var db = fx.CriarContexto())
            await db.Users.Where(u => u.Id == b).ExecuteUpdateAsync(u => u.SetProperty(x => x.Ativo, false), Ct);
        Assert.False((await DetalheAsync(paraB.Id, a))!.PodeResponder);
    }

    [Fact]
    public async Task Arquivada_sai_da_entrada_so_para_quem_arquivou()
    {
        var a = await UsuarioAsync("Ana");
        var b = await UsuarioAsync("Bruno");
        var m = Enviada(a, b, 1);
        m.Arquivar(b, Em(2));
        await SalvarAsync(m);

        Assert.Empty(await CaixaAsync(b, CaixaMensagens.Entrada));
        Assert.Equal([m.Id], (await CaixaAsync(b, CaixaMensagens.Arquivadas)).Select(x => x.Id));
        Assert.Equal([m.Id], (await CaixaAsync(a, CaixaMensagens.Enviados)).Select(x => x.Id));
        Assert.Empty(await CaixaAsync(a, CaixaMensagens.Arquivadas));
        Assert.True((await DetalheAsync(m.Id, b))!.Arquivada);
        Assert.False((await DetalheAsync(m.Id, a))!.Arquivada);
    }

    [Fact]
    public async Task Contagem_de_nao_lidas()
    {
        var a = await UsuarioAsync("Ana");
        var b = await UsuarioAsync("Bruno");
        var lida = Enviada(a, b, 1);
        lida.MarcarLida(b, Em(2));
        var arquivada = Enviada(a, b, 3);
        arquivada.Arquivar(b, Em(4));
        var arquivadaPeloAutor = Enviada(a, b, 5);
        arquivadaPeloAutor.Arquivar(a, Em(6));
        var naoLida = Enviada(a, b, 7);
        var enviadaPorB = Enviada(b, a, 8);
        var rascunho = Mensagem.CriarRascunho(a, TipoMensagem.Aviso, "Rascunho", "Texto", b, null, null, null, Em(9));
        await SalvarAsync(lida, arquivada, arquivadaPeloAutor, naoLida, enviadaPorB, rascunho);

        await using var db = fx.CriarContexto();
        var consultas = new ComunicacaoQueries(db);
        Assert.Equal(2, await consultas.ContarNaoLidasAsync(new UsuarioAtualTeste(b), Ct));
        Assert.Equal(1, await consultas.ContarNaoLidasAsync(new UsuarioAtualTeste(a), Ct));
    }
}
