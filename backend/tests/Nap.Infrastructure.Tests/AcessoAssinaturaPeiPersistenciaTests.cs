using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Peis.Commands;
using Nap.Application.Features.Peis.Queries;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Peis;
using Nap.Infrastructure.Assinatura;
using Nap.Infrastructure.Identidade;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Repositorios;

namespace Nap.Infrastructure.Tests;

public class AcessoAssinaturaPeiPersistenciaTests(SqlServerFixture fx)
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(-4));
    private const string NomeAluno = "João Antônio Souza";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>PEI com a v1 aguardando assinaturas, já gravado.</summary>
    private async Task<(VersaoPei versao, Guid prof)> NovaVersaoAguardandoAsync()
    {
        var prof = await fx.CriarProfissionalAsync();
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, await fx.CriarCursoAsync(), prof, NomeAluno);
        var conteudo = new ConteudoPei("Demanda interna", "Necessidades", null, "Orientações", null, null,
            PeriodoVigencia.Criar(new DateOnly(2026, 8, 1), new DateOnly(2027, 7, 31)));
        var pei = Pei.Criar(aluno.Id, prof, conteudo, prof, Agora);
        var v = pei.Versoes.Single();
        v.EnviarParaRevisao(Agora);
        v.AprovarRevisao(prof, Agora);
        await using var db = fx.CriarContexto();
        db.Add(pei);
        await db.SaveChangesAsync(Ct);
        return (v, prof);
    }

    private static Task<AcessoGeradoDto> GerarAsync(NapDbContext db, Guid versaoId, Guid prof)
    {
        var usuario = new UsuarioAtualTeste(prof);
        var relogio = new RelogioTeste(Agora);
        return new GerarAcessoAssinaturaAlunoHandler(new GerarAcessoAssinaturaAlunoValidator(), new PeiRepository(db),
            new AcessoAssinaturaRepository(db), usuario, relogio, AuditoriaTeste.Criar(db, usuario, relogio), new UnidadeDeTrabalho(db),
            new CacheNulo()).Handle(new GerarAcessoAssinaturaAlunoCommand(versaoId), Ct);
    }

    private static Task<VersaoParaAssinaturaDto> AbrirAsync(NapDbContext db, string token, string codigo) =>
        new AbrirAcessoAssinaturaHandler(new AbrirAcessoAssinaturaValidator(), new AcessoAssinaturaRepository(db),
            new PeiRepository(db), new AlunoRepository(db), new RelogioTeste(Agora.AddMinutes(5)), new UnidadeDeTrabalho(db))
            .Handle(new AbrirAcessoAssinaturaQuery(token, codigo), Ct);

    private static Task AssinarAsync(NapDbContext db, string token, string codigo)
    {
        var relogio = new RelogioTeste(Agora.AddMinutes(5));
        // Requisição anônima: sem HttpContext, a auditoria grava usuário nulo.
        var anonimo = new UsuarioAtual(new HttpContextAccessor());
        return new AssinarPeiAlunoHandler(new AssinarPeiAlunoValidator(), new AcessoAssinaturaRepository(db), new PeiRepository(db),
            new AlunoRepository(db), new AssinaturaDemonstrativa(), relogio, AuditoriaTeste.Criar(db, anonimo, relogio),
            new UnidadeDeTrabalho(db), new CacheNulo()).Handle(new AssinarPeiAlunoCommand(token, codigo, NomeAluno, true), Ct);
    }

    private static string Errado(string codigo) => codigo == "000000" ? "111111" : "000000";

    [Fact]
    public async Task Regerar_acesso_invalida_o_anterior_sem_violar_o_indice_de_acesso_aberto()
    {
        var (v, prof) = await NovaVersaoAguardandoAsync();
        await using (var a = fx.CriarContexto()) await GerarAsync(a, v.Id, prof);
        AcessoGeradoDto segundo;
        await using (var b = fx.CriarContexto()) segundo = await GerarAsync(b, v.Id, prof);

        await using var c = fx.CriarContexto();
        var acessos = await c.AcessosAssinaturaPei.Where(x => x.VersaoId == v.Id).ToListAsync(Ct);
        Assert.Equal(2, acessos.Count);
        Assert.Equal(segundo.AcessoId, acessos.Single(x => x.InvalidadoEm is null).Id);
    }

    [Fact]
    public async Task Geracoes_simultaneas_deixam_um_unico_acesso_aberto()
    {
        var (v, prof) = await NovaVersaoAguardandoAsync();
        await using var b = fx.CriarContexto();
        await using var c = fx.CriarContexto();
        b.Add(AcessoAssinaturaPei.Gerar(v.Id, v.HashConteudo!, prof, Agora).acesso);
        c.Add(AcessoAssinaturaPei.Gerar(v.Id, v.HashConteudo!, prof, Agora).acesso);

        await new UnidadeDeTrabalho(b).SalvarAsync(Ct);
        var erro = await Assert.ThrowsAsync<ConflictException>(() => new UnidadeDeTrabalho(c).SalvarAsync(Ct));
        Assert.Equal("Outro acesso do aluno foi gerado ao mesmo tempo para esta versão. Gere o acesso novamente.", erro.Message);
    }

    [Fact]
    public async Task Tentativa_errada_que_perde_a_corrida_e_recontada()
    {
        var (v, prof) = await NovaVersaoAguardandoAsync();
        AcessoGeradoDto gerado;
        await using (var a = fx.CriarContexto()) gerado = await GerarAsync(a, v.Id, prof);

        // c1 já carregou o acesso (como uma requisição em andamento) quando c2 grava uma tentativa errada.
        await using var c1 = fx.CriarContexto();
        await c1.AcessosAssinaturaPei.SingleAsync(x => x.Id == gerado.AcessoId, Ct);
        await using (var c2 = fx.CriarContexto())
            await Assert.ThrowsAsync<ValidationException>(() => AbrirAsync(c2, gerado.Token, Errado(gerado.Codigo)));

        var erro = await Assert.ThrowsAsync<ValidationException>(() => AbrirAsync(c1, gerado.Token, Errado(gerado.Codigo)));
        Assert.Equal(["Código incorreto. Restam 3 tentativas."], erro.Erros["codigo"]);
        await using var d = fx.CriarContexto();
        Assert.Equal(2, (await d.AcessosAssinaturaPei.SingleAsync(x => x.Id == gerado.AcessoId, Ct)).Tentativas);
    }

    [Fact]
    public async Task Assinaturas_simultaneas_com_o_mesmo_link_gravam_uma_so()
    {
        var (v, prof) = await NovaVersaoAguardandoAsync();
        AcessoGeradoDto gerado;
        await using (var a = fx.CriarContexto()) gerado = await GerarAsync(a, v.Id, prof);

        // c2 carregou acesso e PEI antes de c1 assinar: a segunda requisição trabalha sobre valores antigos.
        await using var c2 = fx.CriarContexto();
        await c2.AcessosAssinaturaPei.SingleAsync(x => x.Id == gerado.AcessoId, Ct);
        await new PeiRepository(c2).ObterPorVersaoAsync(v.Id, Ct);
        await using (var c1 = fx.CriarContexto()) await AssinarAsync(c1, gerado.Token, gerado.Codigo);

        var erro = await Assert.ThrowsAnyAsync<NotFoundException>(() => AssinarAsync(c2, gerado.Token, gerado.Codigo));
        Assert.Equal("Link inválido ou expirado.", erro.Message);

        await using var d = fx.CriarContexto();
        var lida = (await new PeiRepository(d).ObterPorVersaoAsync(v.Id, Ct))!.Versoes.Single();
        var assinatura = Assert.Single(lida.Assinaturas);
        Assert.Equal((PapelAssinatura.Aluno, NomeAluno, v.HashConteudo), (assinatura.Papel, assinatura.NomeSignatario, assinatura.HashConteudo));
        Assert.NotNull((await d.AcessosAssinaturaPei.SingleAsync(x => x.Id == gerado.AcessoId, Ct)).UsadoEm);
        var auditoria = await d.EventosAuditoria.SingleAsync(e => e.Acao == "pei.assinar" && e.RecursoId == gerado.AcessoId, Ct);
        Assert.Null(auditoria.UsuarioId);
    }

    [Fact]
    public async Task Assinatura_que_perde_a_corrida_para_tentativa_errada_assina_na_segunda_gravacao()
    {
        var (v, prof) = await NovaVersaoAguardandoAsync();
        AcessoGeradoDto gerado;
        await using (var a = fx.CriarContexto()) gerado = await GerarAsync(a, v.Id, prof);

        // c2 já carregou acesso e PEI quando c1 grava uma tentativa errada: a gravação de c2 conflita no acesso.
        await using var c2 = fx.CriarContexto();
        await c2.AcessosAssinaturaPei.SingleAsync(x => x.Id == gerado.AcessoId, Ct);
        await new PeiRepository(c2).ObterPorVersaoAsync(v.Id, Ct);
        await using (var c1 = fx.CriarContexto())
            await Assert.ThrowsAsync<ValidationException>(() => AbrirAsync(c1, gerado.Token, Errado(gerado.Codigo)));

        await AssinarAsync(c2, gerado.Token, gerado.Codigo);

        await using var d = fx.CriarContexto();
        var acesso = await d.AcessosAssinaturaPei.SingleAsync(x => x.Id == gerado.AcessoId, Ct);
        Assert.Equal(1, acesso.Tentativas);
        Assert.NotNull(acesso.UsadoEm);
        var lida = (await new PeiRepository(d).ObterPorVersaoAsync(v.Id, Ct))!.Versoes.Single();
        Assert.Equal(PapelAssinatura.Aluno, Assert.Single(lida.Assinaturas).Papel);
        Assert.Equal(1, await d.EventosAuditoria.CountAsync(e => e.Acao == "pei.assinar" && e.RecursoId == gerado.AcessoId, Ct));
    }

    [Fact]
    public async Task Devolver_apos_assinatura_do_profissional_invalida_sem_apagar()
    {
        var (v, prof) = await NovaVersaoAguardandoAsync();
        await using (var a = fx.CriarContexto())
        {
            var versao = (await new PeiRepository(a).ObterPorVersaoAsync(v.Id, Ct))!.Versoes.Single();
            versao.RegistrarAssinatura(PapelAssinatura.Profissional, prof, "Profissional", versao.HashConteudo!, "e1", Agora);
            await new UnidadeDeTrabalho(a).SalvarAsync(Ct);
        }
        await using (var b = fx.CriarContexto())
        {
            var versao = (await new PeiRepository(b).ObterPorVersaoAsync(v.Id, Ct))!.Versoes.Single();
            Assert.Single(versao.Assinaturas);
            versao.Devolver("Rever as adaptações", Agora.AddMinutes(1));
            await new UnidadeDeTrabalho(b).SalvarAsync(Ct);
        }

        await using var c = fx.CriarContexto();
        var final = (await new PeiRepository(c).ObterPorVersaoAsync(v.Id, Ct))!.Versoes.Single();
        Assert.Equal((EstadoVersaoPei.Rascunho, (string?)null), (final.Estado, final.HashConteudo));
        // A assinatura fica como prova do que foi assinado, marcada como invalidada pela devolução.
        var assinatura = await c.Set<AssinaturaPei>().SingleAsync(x => EF.Property<Guid>(x, "VersaoId") == v.Id, Ct);
        Assert.Equal(Agora.AddMinutes(1), assinatura.InvalidadaEm);
        Assert.Null(final.AssinaturaValida(PapelAssinatura.Profissional));
    }
}
