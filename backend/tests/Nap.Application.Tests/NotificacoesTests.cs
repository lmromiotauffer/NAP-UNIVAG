using Nap.Application.Features.Notificacoes.Commands;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Alunos;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Comunicacoes;
using Nap.Domain.Encaminhamentos;
using Nap.Domain.Notificacoes;

namespace Nap.Application.Tests;

public class NotificacoesTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(-3));
    private readonly Guid _curso = Guid.NewGuid();
    private readonly AlunoRepositoryEmMemoria _alunos = new();
    private readonly NotificacaoRepositoryEmMemoria _repo = new();
    private readonly UsuarioQueriesFalso _usuarios = new();
    private readonly MensagemRepositoryEmMemoria _mensagens = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new();
    private readonly Aluno _aluno;

    public NotificacoesTests()
    {
        _aluno = Aluno.Criar("Maria da Silva", Matricula.Criar("2024001"), _curso, 3, new DateOnly(2026, 8, 1),
            Guid.NewGuid(), null, null, Guid.NewGuid(), Agora);
        _alunos.Alunos.Add(_aluno);
    }

    private GerarNotificacoesHandler Handler(EnvioEmailFalso? email = null) =>
        new(_repo, _usuarios, _alunos, _mensagens, email ?? new EnvioEmailFalso(), new RelogioFixo(), _uow);

    private Guid Usuario(string perfil, Guid? cursoId = null, bool ativo = true)
    {
        var id = Guid.NewGuid();
        _usuarios.Usuarios.Add((id, perfil, cursoId, ativo));
        return id;
    }

    private EncaminhamentoCriado Criado(DestinoEncaminhamento destino, Especialidade especialidade = Especialidade.Psicologia) =>
        new(Guid.CreateVersion7(), Agora, Guid.NewGuid(), _aluno.Id, _curso, Guid.NewGuid(), especialidade, destino);

    private RetornoEncaminhamentoRegistrado Retorno(Guid solicitante, Guid autor) =>
        new(Guid.CreateVersion7(), Agora, Guid.NewGuid(), _aluno.Id, solicitante, autor, EstadoEncaminhamento.Acolhido,
            AcaoRetorno.Acolhimento);

    [Fact]
    public async Task Criado_destino_sip_notifica_todos_os_sip_ativos()
    {
        var sip1 = Usuario(Perfis.Sip);
        var sip2 = Usuario(Perfis.Sip);
        Usuario(Perfis.Sip, ativo: false);
        Usuario(Perfis.ProfissionalNap);
        var evento = Criado(DestinoEncaminhamento.Sip);

        var criadas = await Handler().Handle(evento, TestContext.Current.CancellationToken);

        Assert.Equal(2, criadas);
        Assert.Equal(new[] { sip1, sip2 }.Order(), _repo.Notificacoes.Select(n => n.DestinatarioId).Order());
        var n = _repo.Notificacoes[0];
        Assert.Equal("Novo encaminhamento: Maria da Silva", n.Titulo);
        Assert.Equal("Psicologia · SIP", n.Texto);
        Assert.Equal($"/encaminhamentos/{evento.EncaminhamentoId}", n.Link);
        Assert.Equal(evento.EventoId, n.EventoId);
        Assert.Equal("EncaminhamentoCriado", n.Tipo);
    }

    [Fact]
    public async Task Criado_destino_clinica_escola_tambem_notifica_o_sip()
    {
        var sip = Usuario(Perfis.Sip);

        await Handler().Handle(Criado(DestinoEncaminhamento.ClinicaEscola), TestContext.Current.CancellationToken);

        Assert.Equal(sip, Assert.Single(_repo.Notificacoes).DestinatarioId);
    }

    [Fact]
    public async Task Criado_destino_coordenacao_notifica_so_coordenadores_do_curso()
    {
        var coord = Usuario(Perfis.CoordenadorCurso, _curso);
        Usuario(Perfis.CoordenadorCurso, Guid.NewGuid());
        Usuario(Perfis.CoordenadorCurso, _curso, ativo: false);
        Usuario(Perfis.Sip);

        await Handler().Handle(Criado(DestinoEncaminhamento.CoordenacaoCurso, Especialidade.CoordenacaoCurso),
            TestContext.Current.CancellationToken);

        Assert.Equal(coord, Assert.Single(_repo.Notificacoes).DestinatarioId);
        Assert.Equal("Coordenação do curso · Coordenação do curso", _repo.Notificacoes[0].Texto);
    }

    [Fact]
    public async Task Criado_destino_externo_nao_notifica_ninguem()
    {
        Usuario(Perfis.Sip);
        Usuario(Perfis.CoordenadorCurso, _curso);

        var criadas = await Handler().Handle(Criado(DestinoEncaminhamento.Externo), TestContext.Current.CancellationToken);

        Assert.Equal(0, criadas);
        Assert.Empty(_repo.Notificacoes);
    }

    [Fact]
    public async Task Sem_destinatario_ativo_nao_notifica_outra_pessoa()
    {
        Usuario(Perfis.Sip, ativo: false);
        Usuario(Perfis.CoordenadorNap);
        Usuario(Perfis.Administrador);
        Usuario(Perfis.CoordenadorCurso, _curso);

        await Handler().Handle(Criado(DestinoEncaminhamento.Sip), TestContext.Current.CancellationToken);

        Assert.Empty(_repo.Notificacoes);
        Assert.Equal(0, _uow.Salvamentos);
    }

    [Fact]
    public async Task Retorno_notifica_o_solicitante_mas_nao_quando_ele_e_o_autor()
    {
        var solicitante = Usuario(Perfis.ProfissionalNap);
        var autor = Usuario(Perfis.Sip);
        var inativo = Usuario(Perfis.ProfissionalNap, ativo: false);
        var ct = TestContext.Current.CancellationToken;
        var retorno = Retorno(solicitante, autor);

        Assert.Equal(1, await Handler().Handle(retorno, ct));
        Assert.Equal(0, await Handler().Handle(Retorno(solicitante, solicitante), ct));
        Assert.Equal(0, await Handler().Handle(Retorno(inativo, autor), ct));

        var n = Assert.Single(_repo.Notificacoes);
        Assert.Equal(solicitante, n.DestinatarioId);
        Assert.Equal("Retorno do encaminhamento de Maria da Silva", n.Titulo);
        Assert.Equal("Acolhimento: Acolhido", n.Texto);
        Assert.Equal($"/encaminhamentos/{retorno.EncaminhamentoId}", n.Link);
    }

    [Fact]
    public async Task Nome_longo_do_aluno_corta_o_titulo_em_200()
    {
        var aluno = Aluno.Criar(new string('A', 200), Matricula.Criar("2024002"), _curso, 3, new DateOnly(2026, 8, 1),
            Guid.NewGuid(), null, null, Guid.NewGuid(), Agora);
        _alunos.Alunos.Add(aluno);
        var solicitante = Usuario(Perfis.ProfissionalNap);

        await Handler().Handle(new RetornoEncaminhamentoRegistrado(Guid.CreateVersion7(), Agora, Guid.NewGuid(), aluno.Id,
            solicitante, Guid.NewGuid(), EstadoEncaminhamento.Acolhido, AcaoRetorno.Acolhimento), TestContext.Current.CancellationToken);

        var titulo = Assert.Single(_repo.Notificacoes).Titulo;
        Assert.Equal(200, titulo.Length);
        Assert.EndsWith("…", titulo);
    }

    [Fact]
    public async Task Reprocessar_o_mesmo_evento_nao_duplica()
    {
        Usuario(Perfis.Sip);
        Usuario(Perfis.Sip);
        var evento = Criado(DestinoEncaminhamento.Sip);
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(2, await Handler().Handle(evento, ct));
        Usuario(Perfis.Sip); // novo SIP ativo entre as duas execuções: só ele falta
        Assert.Equal(1, await Handler().Handle(evento, ct));
        Assert.Equal(0, await Handler().Handle(evento, ct));
        Assert.Equal(3, _repo.Notificacoes.Count);
    }

    [Fact]
    public async Task Email_generico_registra_estado_enviado()
    {
        var sip = Usuario(Perfis.Sip);
        var email = new EnvioEmailFalso();

        await Handler(email).Handle(Criado(DestinoEncaminhamento.Sip), TestContext.Current.CancellationToken);

        var (destinatario, assunto, corpo) = Assert.Single(email.Enviados);
        Assert.Equal(sip, destinatario);
        Assert.Equal("Você tem uma nova notificação no NAP", assunto);
        Assert.DoesNotContain("Maria", assunto + corpo);
        Assert.Equal(EstadoEmail.Enviado, _repo.Notificacoes[0].EstadoEmail);
    }

    [Fact]
    public async Task Falha_no_email_mantem_a_notificacao_com_estado_falhou()
    {
        Usuario(Perfis.Sip);

        var criadas = await Handler(new EnvioEmailFalso(falhar: true)).Handle(Criado(DestinoEncaminhamento.Sip),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, criadas);
        Assert.Equal(EstadoEmail.Falhou, Assert.Single(_repo.Notificacoes).EstadoEmail);
        Assert.Equal(2, _uow.Salvamentos); // a notificação foi salva antes do e-mail
    }

    [Fact]
    public async Task Notificacao_nao_contem_motivo_nem_observacoes()
    {
        var solicitante = Usuario(Perfis.ProfissionalNap);
        Usuario(Perfis.Sip);
        var hoje = new DateOnly(2026, 10, 2);
        var e = Encaminhamento.Criar(_aluno.Id, _curso, solicitante, Especialidade.Psicologia, DestinoEncaminhamento.Sip,
            hoje, "Ideação suicida relatada", "Resumo pedagógico", "Observação interna sigilosa", hoje, Agora);
        e.RegistrarRetorno(AcaoRetorno.Acolhimento, hoje, "Texto clínico da evolução", Guid.NewGuid(), hoje, Agora);
        var ct = TestContext.Current.CancellationToken;

        foreach (var evento in e.Eventos)
            await Handler().Handle(evento, ct);

        Assert.Equal(2, _repo.Notificacoes.Count);
        foreach (var n in _repo.Notificacoes)
        foreach (var segredo in new[] { "Ideação", "Observação", "clínico", "Resumo" })
            Assert.DoesNotContain(segredo, n.Titulo + n.Texto);
    }

    private MensagemEnviada Enviada(Guid remetente, Guid destinatario, string assunto)
    {
        var m = Mensagem.CriarRascunho(remetente, TipoMensagem.Devolutiva, assunto, "Corpo sigiloso", destinatario, null, null, null, Agora);
        m.Enviar(Agora);
        _mensagens.Adicionar(m);
        return Assert.IsType<MensagemEnviada>(Assert.Single(m.Eventos));
    }

    [Fact]
    public async Task Mensagem_enviada_notifica_o_destinatario()
    {
        var remetente = Usuario(Perfis.ProfissionalNap);
        _usuarios.Nomes[remetente] = "Ana Beatriz";
        var destinatario = Usuario(Perfis.CoordenadorNap);
        Usuario(Perfis.CoordenadorNap);
        var evento = Enviada(remetente, destinatario, "Devolutiva do semestre");

        Assert.Equal(1, await Handler().Handle(evento, TestContext.Current.CancellationToken));

        var n = Assert.Single(_repo.Notificacoes);
        Assert.Equal(destinatario, n.DestinatarioId);
        Assert.Equal("Nova mensagem: Devolutiva do semestre", n.Titulo);
        Assert.Equal("Ana Beatriz · Devolutiva", n.Texto);
        Assert.Equal($"/comunicacoes/{evento.MensagemId}", n.Link);
        Assert.Equal("MensagemEnviada", n.Tipo);
        Assert.DoesNotContain("sigiloso", n.Titulo + n.Texto);
    }

    [Fact]
    public async Task Mensagem_de_remetente_sem_cadastro_mostra_so_o_tipo()
    {
        var destinatario = Usuario(Perfis.CoordenadorNap);

        await Handler().Handle(Enviada(Guid.NewGuid(), destinatario, "Assunto"), TestContext.Current.CancellationToken);

        Assert.Equal("Devolutiva", Assert.Single(_repo.Notificacoes).Texto);
    }

    [Fact]
    public async Task Mensagem_para_destinatario_inativo_nao_notifica()
    {
        var destinatario = Usuario(Perfis.CoordenadorNap, ativo: false);

        var criadas = await Handler().Handle(Enviada(Usuario(Perfis.ProfissionalNap), destinatario, "Assunto"),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, criadas);
        Assert.Empty(_repo.Notificacoes);
    }

    [Fact]
    public async Task Assunto_longo_corta_o_titulo_sem_partir_emoji()
    {
        var destinatario = Usuario(Perfis.CoordenadorNap);
        // O emoji cai exatamente no ponto de corte do título (índices 198 e 199).
        var assunto = new string('A', 183) + "\U0001F600" + new string('B', 15);

        await Handler().Handle(Enviada(Usuario(Perfis.ProfissionalNap), destinatario, assunto), TestContext.Current.CancellationToken);

        Assert.Equal("Nova mensagem: " + new string('A', 183) + "…", Assert.Single(_repo.Notificacoes).Titulo);
    }
}
