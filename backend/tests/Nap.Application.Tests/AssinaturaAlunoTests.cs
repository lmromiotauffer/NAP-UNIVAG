using Nap.Application.Comum;
using Nap.Application.Features.Peis;
using Nap.Application.Features.Peis.Commands;
using Nap.Application.Features.Peis.Queries;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Alunos;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Peis;

namespace Nap.Application.Tests;

public class AssinaturaAlunoTests
{
    private const string LinkInvalido = "Link inválido ou expirado.";
    private readonly RelogioFixo _relogio = new();
    private readonly AlunoRepositoryEmMemoria _alunos = new();
    private readonly PeiRepositoryEmMemoria _peis = new();
    private readonly AcessoAssinaturaRepositoryEmMemoria _acessos = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly CacheConsultasFalso _cache = new();
    private readonly List<string> _ordem = [];
    private readonly UnidadeDeTrabalhoFalsa _uow;
    private readonly Guid _prof = Guid.NewGuid();
    private readonly VersaoPei _versao;
    private readonly AcessoAssinaturaPei _acesso;
    private readonly string _token;
    private readonly string _codigo;

    public AssinaturaAlunoTests()
    {
        _uow = new UnidadeDeTrabalhoFalsa(_ordem);
        var aluno = Aluno.Criar("Maria José da Silva", Matricula.Criar("2024001"), Guid.NewGuid(), 3, new DateOnly(2026, 8, 1),
            _prof, null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        _alunos.Alunos.Add(aluno);
        var antes = _relogio.Agora.AddDays(-1);
        var conteudo = new ConteudoPei("Demanda sigilosa do NAP", "Necessidades", "Adaptações", "Orientações", null, null,
            PeriodoVigencia.Criar(new DateOnly(2026, 8, 1), new DateOnly(2027, 7, 31)));
        var pei = Pei.Criar(aluno.Id, _prof, conteudo, _prof, antes);
        _peis.Adicionar(pei);
        _versao = pei.Versoes.Single();
        _versao.EnviarParaRevisao(antes);
        _versao.AprovarRevisao(Guid.NewGuid(), CalculadoraHashPei.Calcular(_versao.Conteudo), antes);
        (_acesso, _token, _codigo) = AcessoAssinaturaPei.Gerar(_versao.Id, _versao.HashConteudo!, _prof, _relogio.Agora.AddHours(-1));
        _acessos.Adicionar(_acesso);
    }

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private string CodigoErrado => _codigo == "000000" ? "111111" : "000000";

    private Task<VersaoParaAssinaturaDto> Abrir(string token, string codigo, IRelogio? relogio = null) =>
        new AbrirAcessoAssinaturaHandler(new AbrirAcessoAssinaturaValidator(), _acessos, _peis, _alunos, relogio ?? _relogio, _uow)
            .Handle(new AbrirAcessoAssinaturaQuery(token, codigo), Ct);

    private Task Assinar(string codigo, string nome = "Maria José da Silva", bool aceite = true) =>
        new AssinarPeiAlunoHandler(new AssinarPeiAlunoValidator(), _acessos, _peis, _alunos, new ServicoAssinaturaFalso(),
            _relogio, _auditoria, _uow, _cache).Handle(new AssinarPeiAlunoCommand(_token, codigo, nome, aceite), Ct);

    private static async Task Da404(Func<Task> acao)
    {
        var erro = await Assert.ThrowsAnyAsync<NotFoundException>(acao);
        Assert.Equal(LinkInvalido, erro.Message);
    }

    [Fact]
    public async Task Abrir_com_codigo_certo_mostra_a_versao()
    {
        var dto = await Abrir(_token, _codigo);

        Assert.Equal(("Maria José da Silva", 1, _versao.HashConteudo, _acesso.ExpiraEm),
            (dto.Aluno, dto.Numero, dto.HashConteudo, dto.ExpiraEm));
        Assert.Equal(new ConteudoPeiDto(null, "Necessidades", "Adaptações", "Orientações", null, null, new DateOnly(2026, 8, 1),
            new DateOnly(2027, 7, 31)), dto.Conteudo);
        Assert.Equal("Assinatura demonstrativa — não certificada (pendência institucional)", dto.AvisoAssinatura);
        // Abrir não consome o acesso nem grava nada.
        Assert.Null(_acesso.UsadoEm);
        Assert.Equal(0, _uow.Salvamentos);
        Assert.Empty(_auditoria.Registros);
    }

    [Fact]
    public async Task Token_desconhecido_expirado_ou_usado_dao_o_mesmo_404()
    {
        await Da404(() => Abrir("token-desconhecido", _codigo));
        await Da404(() => Abrir(_token, _codigo, new RelogioFixo { Agora = _acesso.ExpiraEm }));

        _acesso.MarcarUsado(_relogio.Agora);
        await Da404(() => Abrir(_token, _codigo));
        await Da404(() => Assinar(_codigo));

        var (invalidado, token, codigo) = AcessoAssinaturaPei.Gerar(_versao.Id, _versao.HashConteudo!, _prof, _relogio.Agora);
        invalidado.Invalidar(_relogio.Agora);
        _acessos.Adicionar(invalidado);
        await Da404(() => Abrir(token, codigo));
        Assert.Equal(0, _uow.Salvamentos);
    }

    [Fact]
    public async Task Codigo_errado_conta_tentativa_e_informa_restantes()
    {
        var erro = await Assert.ThrowsAsync<ValidationException>(() => Abrir(_token, CodigoErrado));

        Assert.Equal(["Código incorreto. Restam 4 tentativas."], erro.Erros["codigo"]);
        Assert.Equal(1, _acesso.Tentativas);
        // Gravado fora de transação, antes da exceção: um rollback desfaria a contagem.
        Assert.Equal(["salvar"], _ordem);

        var noAssinar = await Assert.ThrowsAsync<ValidationException>(() => Assinar(CodigoErrado));
        Assert.Equal(["Código incorreto. Restam 3 tentativas."], noAssinar.Erros["codigo"]);
        Assert.Null(_versao.Assinaturas.SingleOrDefault());
        Assert.Empty(_auditoria.Registros);
    }

    [Fact]
    public async Task Quinta_tentativa_invalida_e_depois_ate_o_codigo_certo_da_404()
    {
        for (var restantes = 4; restantes >= 1; restantes--)
            await Assert.ThrowsAsync<ValidationException>(() => Abrir(_token, CodigoErrado));

        await Da404(() => Abrir(_token, CodigoErrado));
        Assert.NotNull(_acesso.InvalidadoEm);
        Assert.Equal(5, _uow.Salvamentos);

        await Da404(() => Abrir(_token, _codigo));
        await Da404(() => Assinar(_codigo));
    }

    [Fact]
    public async Task Versao_devolvida_apos_gerar_o_link_da_404()
    {
        // Sem passar pelo handler de devolução (que invalidaria o acesso): o link confere estado e hash da versão.
        _versao.Devolver("Rever as adaptações", _relogio.Agora);
        await Da404(() => Abrir(_token, _codigo));

        _versao.AtualizarConteudo(new ConteudoPei(null, "Outras necessidades", null, null, null, null,
            PeriodoVigencia.Criar(new DateOnly(2026, 8, 1), new DateOnly(2027, 7, 31))), _relogio.Agora);
        _versao.EnviarParaRevisao(_relogio.Agora);
        _versao.AprovarRevisao(Guid.NewGuid(), CalculadoraHashPei.Calcular(_versao.Conteudo), _relogio.Agora);
        Assert.NotEqual(_acesso.HashConteudo, _versao.HashConteudo);
        await Da404(() => Abrir(_token, _codigo));
        await Da404(() => Assinar(_codigo));
        Assert.Equal(0, _acesso.Tentativas);
    }

    [Fact]
    public async Task Assinar_exige_nome_do_aluno_e_aceite()
    {
        var semAceite = await Assert.ThrowsAsync<ValidationException>(() => Assinar(_codigo, aceite: false));
        Assert.True(semAceite.Erros.ContainsKey("aceite"));
        var curto = await Assert.ThrowsAsync<ValidationException>(() => Assinar(_codigo, "  M "));
        Assert.True(curto.Erros.ContainsKey("nomeCompleto"));
        var outro = await Assert.ThrowsAsync<ValidationException>(() => Assinar(_codigo, "Maria da Silva"));
        Assert.Equal(["Digite seu nome completo como está no cadastro."], outro.Erros["nomeCompleto"]);
        Assert.Empty(_versao.Assinaturas);
        Assert.Null(_acesso.UsadoEm);
        Assert.Equal(0, _acesso.Tentativas);

        // Caixa, acentos e espaços extras não importam.
        await Assinar(_codigo, "  maria   JOSE da  sílva ");
        Assert.Single(_versao.Assinaturas);
    }

    [Fact]
    public async Task Nome_com_apostrofo_tipografico_e_travessao_equivale_ao_ascii()
    {
        _alunos.Alunos.Single().AtualizarDados("Ana D'Ávila Souza-Lima", 3, null, null, _prof);

        await Assinar(_codigo, "ana d’avila souza–lima");

        Assert.Single(_versao.Assinaturas);
    }

    [Fact]
    public async Task Perder_a_corrida_de_novo_devolve_mensagem_amigavel()
    {
        var uow = new UnidadeDeTrabalhoFalsa(_ordem) { ConflitoAoSalvar = "O registro foi alterado por outra pessoa. Recarregue e tente novamente" };
        var acessos = new AcessoAssinaturaRepositoryEmMemoria { RecarregarDesfazUso = true };
        acessos.Adicionar(_acesso);
        var handler = new AssinarPeiAlunoHandler(new AssinarPeiAlunoValidator(), acessos, _peis, _alunos, new ServicoAssinaturaFalso(),
            _relogio, _auditoria, uow, _cache);

        var erro = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new AssinarPeiAlunoCommand(_token, _codigo, "Maria José da Silva", true), Ct));

        Assert.Equal("Não foi possível registrar agora. Tente novamente.", erro.Message);
    }

    [Fact]
    public async Task Assinatura_usa_o_hash_da_versao_e_marca_uso()
    {
        await Assinar(_codigo);

        var assinatura = Assert.Single(_versao.Assinaturas);
        Assert.Equal((PapelAssinatura.Aluno, (Guid?)null, "Maria José da Silva", _versao.HashConteudo, "Demonstrativa"),
            (assinatura.Papel, assinatura.SignatarioId, assinatura.NomeSignatario, assinatura.HashConteudo, assinatura.Metodo));
        Assert.Equal($"evidencia-Aluno-{_versao.HashConteudo}", assinatura.Evidencia);
        Assert.Equal(_relogio.Agora, _acesso.UsadoEm);
        Assert.Equal(EstadoVersaoPei.AguardandoAssinaturas, _versao.Estado);
        Assert.Equal([("pei.assinar", "AcessoAssinaturaPei", (Guid?)_acesso.Id)], _auditoria.Registros);
        Assert.Equal(["salvar"], _ordem);
        Assert.Equal(1, _cache.Invalidacoes);
    }

    [Fact]
    public async Task Assinatura_depois_da_do_profissional_deixa_a_versao_assinada_sem_publicar()
    {
        _versao.RegistrarAssinatura(PapelAssinatura.Profissional, _prof, "Ana Beatriz", _versao.HashConteudo!, "e1",
            _relogio.Agora);

        await Assinar(_codigo);

        Assert.Equal(EstadoVersaoPei.Assinada, _versao.Estado);
        Assert.Null(_versao.PublicadaEm);
    }

    [Fact]
    public async Task Segunda_assinatura_com_o_mesmo_link_da_404()
    {
        await Assinar(_codigo);

        await Da404(() => Assinar(_codigo));
        await Da404(() => Abrir(_token, _codigo));
        Assert.Single(_versao.Assinaturas);
    }

    [Fact]
    public void Nenhum_dado_alem_da_versao_no_dto()
    {
        Assert.Equal(["Aluno", "AvisoAssinatura", "Conteudo", "ExpiraEm", "HashConteudo", "Numero"],
            typeof(VersaoParaAssinaturaDto).GetProperties().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task Demanda_interna_nunca_chega_ao_aluno()
    {
        var dto = await Abrir(_token, _codigo);

        Assert.Null(dto.Conteudo.DemandaInterna);
        Assert.DoesNotContain("Demanda sigilosa", dto.ToString());
    }
}
