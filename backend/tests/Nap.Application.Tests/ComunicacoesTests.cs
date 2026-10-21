using Nap.Application.Comum;
using Nap.Application.Features.Comunicacoes;
using Nap.Application.Features.Comunicacoes.Commands;
using Nap.Application.Features.Comunicacoes.Queries;
using Nap.Application.Features.Notificacoes.Commands;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Alunos;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Comunicacoes;
using Nap.Domain.Encaminhamentos;
using Nap.Domain.Notificacoes;

namespace Nap.Application.Tests;

public class ComunicacoesTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 2);
    private readonly Guid _curso = Guid.NewGuid();
    private readonly Guid _outroCurso = Guid.NewGuid();
    private readonly AlunoRepositoryEmMemoria _alunos = new();
    private readonly EncaminhamentoRepositoryEmMemoria _encaminhamentos = new();
    private readonly MensagemRepositoryEmMemoria _repo = new();
    private readonly UsuarioQueriesFalso _usuarios = new();
    private readonly CursoQueriesFalso _cursos;
    private readonly UnidadeDeTrabalhoFalsa _uow = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly NotificacaoRepositoryEmMemoria _notificacoes = new();
    private readonly Aluno _aluno;
    private readonly Aluno _outroAluno;

    public ComunicacoesTests()
    {
        _cursos = new(_curso, _outroCurso);
        _aluno = NovoAluno("2024001", _curso);
        _outroAluno = NovoAluno("2024002", _outroCurso);
    }

    private Aluno NovoAluno(string matricula, Guid curso)
    {
        var a = Aluno.Criar("Maria da Silva", Matricula.Criar(matricula), curso, 3, new DateOnly(2026, 8, 1), Guid.NewGuid(),
            null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        _alunos.Alunos.Add(a);
        return a;
    }

    private Encaminhamento Encaminhar(Aluno aluno, DestinoEncaminhamento destino)
    {
        var especialidade = destino == DestinoEncaminhamento.CoordenacaoCurso ? Especialidade.CoordenacaoCurso : Especialidade.Psicologia;
        var e = Encaminhamento.Criar(aluno.Id, aluno.CursoAtualId, Guid.NewGuid(), especialidade, destino, Hoje, "motivo", "resumo",
            null, Hoje, DateTimeOffset.UtcNow);
        _encaminhamentos.Encaminhamentos.Add(e);
        return e;
    }

    /// <summary>Usuário cadastrado (destinatário possível).</summary>
    private Guid Usuario(string perfil, Guid? cursoId = null, bool ativo = true, string? nome = null)
    {
        var id = Guid.NewGuid();
        _usuarios.Usuarios.Add((id, perfil, cursoId, ativo));
        _usuarios.Nomes[id] = nome ?? $"{perfil} {id:N}";
        return id;
    }

    /// <summary>Usuário autenticado, também cadastrado.</summary>
    private UsuarioAtualFalso Eu(string perfil = Perfis.ProfissionalNap, Guid? cursoId = null, string? nome = null)
    {
        var u = new UsuarioAtualFalso(perfil, cursoId);
        _usuarios.Usuarios.Add((u.Id, perfil, cursoId, true));
        _usuarios.Nomes[u.Id] = nome ?? perfil;
        return u;
    }

    private void Inativar(Guid id)
    {
        var i = _usuarios.Usuarios.FindIndex(u => u.Id == id);
        _usuarios.Usuarios[i] = _usuarios.Usuarios[i] with { Ativo = false };
    }

    private EnviarMensagemHandler Enviar(IUsuarioAtual u) =>
        new(new EnviarMensagemValidator(), _repo, _alunos, _encaminhamentos, _cursos, _usuarios, u, new RelogioFixo(), _auditoria, _uow);

    private CriarRascunhoHandler CriarRascunho(IUsuarioAtual u) =>
        new(new CriarRascunhoValidator(), _repo, _alunos, _encaminhamentos, _cursos, _usuarios, u, new RelogioFixo(), _uow);
    private AtualizarRascunhoHandler AtualizarRascunho(IUsuarioAtual u) =>
        new(new AtualizarRascunhoValidator(), _repo, _alunos, _encaminhamentos, _cursos, _usuarios, u, new RelogioFixo(), _uow);
    private DescartarRascunhoHandler DescartarRascunho(IUsuarioAtual u) => new(_repo, u, _uow);
    private ResponderMensagemHandler Responder(IUsuarioAtual u) =>
        new(new ResponderMensagemValidator(), _repo, _alunos, _encaminhamentos, _usuarios, u, new RelogioFixo(), _auditoria, _uow);
    private MarcarMensagemLidaHandler MarcarLida(IUsuarioAtual u) => new(_repo, _notificacoes, u, new RelogioFixo(), _uow);
    private ArquivarMensagemHandler Arquivar(IUsuarioAtual u) => new(_repo, u, new RelogioFixo(), _uow);
    private DesarquivarMensagemHandler Desarquivar(IUsuarioAtual u) => new(_repo, u, _uow);
    private ListarDestinatariosHandler Destinatarios(IUsuarioAtual u) => new(_alunos, _encaminhamentos, _cursos, _usuarios, u);

    private static EnviarMensagemCommand Mensagem(Guid destinatario, Guid? alunoId = null, Guid? encaminhamentoId = null,
        Guid? cursoId = null, TipoMensagem tipo = TipoMensagem.Aviso, Guid? rascunhoId = null) =>
        new(rascunhoId, destinatario, tipo, "Assunto da mensagem", "Corpo da mensagem", cursoId, alunoId, encaminhamentoId);

    private static CriarRascunhoCommand Rascunho(Guid? destinatario, Guid? alunoId = null) =>
        new(TipoMensagem.Aviso, "Rascunho", "Texto", destinatario, null, alunoId, null);

    private static async Task CampoInvalido(string campo, Func<Task> acao)
    {
        var erro = await Assert.ThrowsAsync<ValidationException>(acao);
        Assert.Equal(campo, Assert.Single(erro.Erros.Keys));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Mensagem já enviada de <paramref name="autor"/> para <paramref name="destinatario"/>.</summary>
    private async Task<Guid> EnviadaAsync(IUsuarioAtual autor, Guid destinatario) =>
        await Enviar(autor).Handle(Mensagem(destinatario), Ct);

    [Fact]
    public async Task Envia_sem_vinculo_para_qualquer_perfil_exceto_gestao()
    {
        var eu = Eu();
        var aceitos = new[]
        {
            Usuario(Perfis.Administrador), Usuario(Perfis.CoordenadorNap), Usuario(Perfis.ProfissionalNap),
            Usuario(Perfis.CoordenadorCurso, _outroCurso), Usuario(Perfis.Sip),
        };

        foreach (var d in aceitos)
            await Enviar(eu).Handle(Mensagem(d), Ct);
        await CampoInvalido("destinatarioId", () => Enviar(eu).Handle(Mensagem(Usuario(Perfis.Gestao)), Ct));
        await CampoInvalido("destinatarioId", () => Enviar(eu).Handle(Mensagem(eu.Id), Ct));
        await CampoInvalido("destinatarioId", () => Enviar(eu).Handle(Mensagem(Guid.NewGuid()), Ct));

        Assert.Equal(aceitos, _repo.Mensagens.Select(m => m.DestinatarioId!.Value));
        Assert.All(_repo.Mensagens, m => Assert.Equal(EstadoMensagem.Enviada, m.Estado));
        Assert.All(_auditoria.Registros, r => Assert.Equal("mensagem.enviar", r.Acao));
        Assert.Equal(aceitos.Length, _uow.Salvamentos);
    }

    [Fact]
    public async Task Coordenador_de_outro_curso_nao_recebe_mensagem_sobre_aluno()
    {
        var eu = Eu();
        var coordOutro = Usuario(Perfis.CoordenadorCurso, _outroCurso);
        var coordDoAluno = Usuario(Perfis.CoordenadorCurso, _curso);

        await CampoInvalido("destinatarioId", () => Enviar(eu).Handle(Mensagem(coordOutro, _aluno.Id), Ct));
        Assert.Empty(_repo.Mensagens);

        await Enviar(eu).Handle(Mensagem(coordDoAluno, _aluno.Id), Ct);
        var m = Assert.Single(_repo.Mensagens);
        Assert.Equal((_aluno.Id, _curso), (m.AlunoId!.Value, m.CursoId!.Value));
    }

    [Fact]
    public async Task So_curso_aceita_equipe_e_coordenador_do_curso_e_coordenador_so_usa_o_proprio()
    {
        var eu = Eu();
        await Enviar(eu).Handle(Mensagem(Usuario(Perfis.CoordenadorCurso, _curso), cursoId: _curso), Ct);
        await Enviar(eu).Handle(Mensagem(Usuario(Perfis.CoordenadorNap), cursoId: _curso), Ct);
        await CampoInvalido("destinatarioId", () => Enviar(eu).Handle(Mensagem(Usuario(Perfis.Sip), cursoId: _curso), Ct));
        await CampoInvalido("destinatarioId", () =>
            Enviar(eu).Handle(Mensagem(Usuario(Perfis.CoordenadorCurso, _outroCurso), cursoId: _curso), Ct));
        await CampoInvalido("cursoId", () => Enviar(eu).Handle(Mensagem(Usuario(Perfis.ProfissionalNap), cursoId: Guid.NewGuid()), Ct));

        var coordenador = Eu(Perfis.CoordenadorCurso, _curso);
        await CampoInvalido("cursoId", () => Enviar(coordenador).Handle(Mensagem(Usuario(Perfis.ProfissionalNap), cursoId: _outroCurso), Ct));
        Assert.Equal(2, _repo.Mensagens.Count);
    }

    [Fact]
    public async Task Sip_so_recebe_com_encaminhamento_sip()
    {
        var eu = Eu();
        var sip = Usuario(Perfis.Sip);

        await CampoInvalido("destinatarioId", () => Enviar(eu).Handle(Mensagem(sip, _aluno.Id), Ct));
        await CampoInvalido("destinatarioId", () =>
            Enviar(eu).Handle(Mensagem(sip, _aluno.Id, Encaminhar(_aluno, DestinoEncaminhamento.Externo).Id), Ct));
        await Enviar(eu).Handle(Mensagem(sip, _aluno.Id, Encaminhar(_aluno, DestinoEncaminhamento.Sip).Id), Ct);
        await Enviar(eu).Handle(Mensagem(sip, _aluno.Id, Encaminhar(_aluno, DestinoEncaminhamento.ClinicaEscola).Id), Ct);

        Assert.Equal(2, _repo.Mensagens.Count);
    }

    [Fact]
    public async Task Sip_nao_vincula_aluno_sem_encaminhamento()
    {
        var sip = Eu(Perfis.Sip);
        var prof = Usuario(Perfis.ProfissionalNap);
        var encaminhamento = Encaminhar(_aluno, DestinoEncaminhamento.Sip);

        await CampoInvalido("alunoId", () => Enviar(sip).Handle(Mensagem(prof, _aluno.Id), Ct));
        // Encaminhamento fora do alcance do SIP também não serve de ponte para o aluno.
        await CampoInvalido("encaminhamentoId", () =>
            Enviar(sip).Handle(Mensagem(prof, _aluno.Id, Encaminhar(_aluno, DestinoEncaminhamento.Externo).Id), Ct));
        await Enviar(sip).Handle(Mensagem(prof, _aluno.Id, encaminhamento.Id, tipo: TipoMensagem.RetornoEncaminhamento), Ct);

        Assert.Equal(encaminhamento.Id, Assert.Single(_repo.Mensagens).EncaminhamentoId);
    }

    [Fact]
    public async Task Coordenador_so_vincula_aluno_do_proprio_curso()
    {
        var coordenador = Eu(Perfis.CoordenadorCurso, _curso);
        var prof = Usuario(Perfis.ProfissionalNap);

        await CampoInvalido("alunoId", () => Enviar(coordenador).Handle(Mensagem(prof, _outroAluno.Id), Ct));
        await CampoInvalido("alunoId", () => Enviar(coordenador).Handle(Mensagem(prof, Guid.NewGuid()), Ct));
        await Enviar(coordenador).Handle(Mensagem(prof, _aluno.Id), Ct);

        Assert.Single(_repo.Mensagens);
    }

    [Fact]
    public async Task Encaminhamento_de_outro_aluno_e_rejeitado()
    {
        var eu = Eu();
        var encaminhamento = Encaminhar(_outroAluno, DestinoEncaminhamento.Sip);

        await CampoInvalido("encaminhamentoId", () =>
            Enviar(eu).Handle(Mensagem(Usuario(Perfis.Sip), _aluno.Id, encaminhamento.Id), Ct));
        await CampoInvalido("encaminhamentoId", () =>
            Enviar(eu).Handle(Mensagem(Usuario(Perfis.Sip), _aluno.Id, Guid.NewGuid()), Ct));
        Assert.Empty(_repo.Mensagens);
    }

    [Fact]
    public async Task Encaminhamento_sem_aluno_grava_o_aluno_dele_e_o_curso_atual()
    {
        var eu = Eu();
        var encaminhamento = Encaminhar(_aluno, DestinoEncaminhamento.Sip);

        await Enviar(eu).Handle(Mensagem(Usuario(Perfis.Sip), encaminhamentoId: encaminhamento.Id), Ct);

        var m = Assert.Single(_repo.Mensagens);
        Assert.Equal((_aluno.Id, _curso, encaminhamento.Id), (m.AlunoId!.Value, m.CursoId!.Value, m.EncaminhamentoId!.Value));
    }

    [Fact]
    public async Task Curso_divergente_do_aluno_e_rejeitado()
    {
        var eu = Eu();
        var prof = Usuario(Perfis.ProfissionalNap);

        await CampoInvalido("cursoId", () => Enviar(eu).Handle(Mensagem(prof, _aluno.Id, cursoId: _outroCurso), Ct));
        await Enviar(eu).Handle(Mensagem(prof, _aluno.Id, cursoId: _curso), Ct);

        Assert.Equal(_curso, Assert.Single(_repo.Mensagens).CursoId);
    }

    [Fact]
    public async Task Tipos_que_exigem_vinculo_sao_validados()
    {
        var eu = Eu();
        var prof = Usuario(Perfis.ProfissionalNap);

        await CampoInvalido("tipo", () => Enviar(eu).Handle(Mensagem(prof, _aluno.Id, tipo: TipoMensagem.EnvioPei), Ct));
        await CampoInvalido("encaminhamentoId", () =>
            Enviar(eu).Handle(Mensagem(prof, _aluno.Id, tipo: TipoMensagem.RetornoEncaminhamento), Ct));
        Assert.Empty(_repo.Mensagens);
    }

    [Fact]
    public async Task Rascunho_de_outro_usuario_retorna_404()
    {
        var autor = Eu();
        var destinatario = Eu(Perfis.CoordenadorNap);
        var id = await CriarRascunho(autor).Handle(Rascunho(destinatario.Id), Ct);
        var terceiro = Eu();

        foreach (var outro in new[] { destinatario, terceiro })
        {
            await Assert.ThrowsAsync<NotFoundException>(() => AtualizarRascunho(outro).Handle(
                new(id, TipoMensagem.Aviso, "Outro assunto", "Outro texto", null, null, null, null, "v1"), Ct));
            await Assert.ThrowsAsync<NotFoundException>(() => DescartarRascunho(outro).Handle(new(id), Ct));
            await Assert.ThrowsAsync<NotFoundException>(() => MarcarLida(outro).Handle(new(id), Ct));
            await Assert.ThrowsAsync<NotFoundException>(() => Arquivar(outro).Handle(new(id), Ct));
            await Assert.ThrowsAsync<NotFoundException>(() => Responder(outro).Handle(new(id, "resposta"), Ct));
        }

        Assert.Equal("Rascunho", Assert.Single(_repo.Mensagens).Assunto);
        await DescartarRascunho(autor).Handle(new(id), Ct);
        Assert.Empty(_repo.Mensagens);
    }

    [Fact]
    public async Task Rascunho_com_aluno_inexistente_ou_invisivel_e_400()
    {
        var prof = Usuario(Perfis.ProfissionalNap);
        await CampoInvalido("alunoId", () => CriarRascunho(Eu()).Handle(Rascunho(prof, Guid.NewGuid()), Ct));
        await CampoInvalido("alunoId", () => CriarRascunho(Eu(Perfis.Sip)).Handle(Rascunho(prof, _aluno.Id), Ct));
        var coordenador = Eu(Perfis.CoordenadorCurso, _outroCurso);
        await CampoInvalido("alunoId", () => CriarRascunho(coordenador).Handle(Rascunho(prof, _aluno.Id), Ct));

        var id = await CriarRascunho(coordenador).Handle(Rascunho(prof, _outroAluno.Id), Ct);
        await CampoInvalido("alunoId", () => AtualizarRascunho(coordenador).Handle(
            new(id, TipoMensagem.Aviso, "Rascunho", "Texto", prof, null, _aluno.Id, null, "v1"), Ct));

        var m = Assert.Single(_repo.Mensagens);
        Assert.Equal((_outroAluno.Id, _outroCurso), (m.AlunoId!.Value, m.CursoId!.Value));
    }

    [Fact]
    public async Task Rascunho_com_destinatario_inexistente_ou_inativo_e_400()
    {
        var eu = Eu();
        await CampoInvalido("destinatarioId", () => CriarRascunho(eu).Handle(Rascunho(Guid.NewGuid()), Ct));
        await CampoInvalido("destinatarioId", () => CriarRascunho(eu).Handle(Rascunho(Usuario(Perfis.CoordenadorNap, ativo: false)), Ct));
        await CampoInvalido("destinatarioId", () => CriarRascunho(eu).Handle(Rascunho(eu.Id), Ct));
        var id = await CriarRascunho(eu).Handle(Rascunho(null), Ct);
        await CampoInvalido("destinatarioId", () => AtualizarRascunho(eu).Handle(
            new(id, TipoMensagem.Aviso, "Rascunho", "Texto", Guid.NewGuid(), null, null, null, "v1"), Ct));

        // Quem pode receber só é conferido no envio: SIP sem encaminhamento cabe no rascunho, mas não é enviado.
        var sip = Usuario(Perfis.Sip);
        var rascunho = await CriarRascunho(eu).Handle(Rascunho(sip, _aluno.Id), Ct);
        await CampoInvalido("destinatarioId", () => Enviar(eu).Handle(Mensagem(sip, _aluno.Id, rascunhoId: rascunho), Ct));
        Assert.All(_repo.Mensagens, m => Assert.Equal(EstadoMensagem.Rascunho, m.Estado));
    }

    [Fact]
    public async Task Autor_atualiza_o_rascunho_com_a_versao()
    {
        var autor = Eu();
        var id = await CriarRascunho(autor).Handle(Rascunho(null), Ct);

        await AtualizarRascunho(autor).Handle(new(id, TipoMensagem.Devolutiva, "Novo assunto", "Novo texto", null, null, null, null, "v1"), Ct);

        var m = Assert.Single(_repo.Mensagens);
        Assert.Equal(("Novo assunto", TipoMensagem.Devolutiva, EstadoMensagem.Rascunho), (m.Assunto, m.Tipo, m.Estado));
        Assert.Equal(["v1"], _uow.VersoesExigidas);
        Assert.Empty(m.Eventos);
    }

    [Fact]
    public async Task Enviar_rascunho_alheio_retorna_404()
    {
        var autor = Eu();
        var outro = Eu();
        var destinatario = Usuario(Perfis.CoordenadorNap);
        var id = await CriarRascunho(autor).Handle(Rascunho(destinatario), Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => Enviar(outro).Handle(Mensagem(destinatario, rascunhoId: id), Ct));
        Assert.Equal(EstadoMensagem.Rascunho, Assert.Single(_repo.Mensagens).Estado);

        Assert.Equal(id, await Enviar(autor).Handle(Mensagem(destinatario, rascunhoId: id), Ct));
        var m = Assert.Single(_repo.Mensagens);
        Assert.Equal(("Assunto da mensagem", EstadoMensagem.Enviada), (m.Assunto, m.Estado));
    }

    [Fact]
    public async Task Enviada_nao_pode_ser_alterada()
    {
        var autor = Eu();
        var destinatario = Usuario(Perfis.CoordenadorNap);
        var id = await EnviadaAsync(autor, destinatario);

        await Assert.ThrowsAsync<DomainException>(() => AtualizarRascunho(autor).Handle(
            new(id, TipoMensagem.Aviso, "Outro assunto", "Outro texto", destinatario, null, null, null, "v1"), Ct));
        await Assert.ThrowsAsync<DomainException>(() => DescartarRascunho(autor).Handle(new(id), Ct));
        await Assert.ThrowsAsync<DomainException>(() => Enviar(autor).Handle(Mensagem(destinatario, rascunhoId: id), Ct));

        Assert.Equal("Assunto da mensagem", Assert.Single(_repo.Mensagens).Assunto);
    }

    [Fact]
    public async Task Autor_nao_marca_lida()
    {
        var autor = Eu();
        var destinatario = Eu(Perfis.CoordenadorNap);
        var id = await EnviadaAsync(autor, destinatario.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => MarcarLida(autor).Handle(new(id), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => MarcarLida(Eu()).Handle(new(id), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => MarcarLida(destinatario).Handle(new(Guid.NewGuid()), Ct));
        Assert.Null(_repo.Mensagens[0].LidaEm);

        await MarcarLida(destinatario).Handle(new(id), Ct);
        Assert.NotNull(_repo.Mensagens[0].LidaEm);
    }

    [Fact]
    public async Task Marcar_lida_tambem_marca_a_notificacao_da_mensagem()
    {
        var autor = Eu();
        var destinatario = Eu(Perfis.CoordenadorNap);
        var id = await EnviadaAsync(autor, destinatario.Id);
        var outraId = await EnviadaAsync(autor, destinatario.Id);
        Notificacao Notificar(Guid usuarioId, Guid mensagemId) =>
            Notificacao.Criar(usuarioId, Guid.NewGuid(), nameof(MensagemEnviada), "Nova mensagem", "Texto", $"/comunicacoes/{mensagemId}",
                new RelogioFixo().Agora);
        var daMensagem = Notificar(destinatario.Id, id);
        var daOutra = Notificar(destinatario.Id, outraId);
        var deOutroUsuario = Notificar(autor.Id, id);
        _notificacoes.Adicionar(daMensagem);
        _notificacoes.Adicionar(daOutra);
        _notificacoes.Adicionar(deOutroUsuario);

        await MarcarLida(destinatario).Handle(new(id), Ct);

        Assert.NotNull(daMensagem.LidaEm);
        Assert.Null(daOutra.LidaEm);
        Assert.Null(deOutroUsuario.LidaEm);
    }

    [Fact]
    public async Task Cada_participante_arquiva_para_si()
    {
        var autor = Eu();
        var destinatario = Eu(Perfis.CoordenadorNap);
        var id = await EnviadaAsync(autor, destinatario.Id);

        await Arquivar(destinatario).Handle(new(id), Ct);
        await Assert.ThrowsAsync<NotFoundException>(() => Arquivar(Eu()).Handle(new(id), Ct));
        var m = _repo.Mensagens[0];
        Assert.NotNull(m.ArquivadaPeloDestinatarioEm);
        Assert.Null(m.ArquivadaPeloAutorEm);

        await Desarquivar(destinatario).Handle(new(id), Ct);
        Assert.Null(m.ArquivadaPeloDestinatarioEm);
    }

    [Fact]
    public async Task Resposta_de_nao_participante_retorna_404()
    {
        var autor = Eu();
        var destinatario = Eu(Perfis.CoordenadorCurso, _curso);
        var original = await Enviar(autor).Handle(Mensagem(destinatario.Id, _aluno.Id), Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => Responder(Eu()).Handle(new(original, "Intromissão"), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => Responder(destinatario).Handle(new(Guid.NewGuid(), "Resposta"), Ct));

        var id = await Responder(destinatario).Handle(new(original, "Resposta"), Ct);
        var r = _repo.Mensagens.Single(m => m.Id == id);
        Assert.Equal((autor.Id, original, _aluno.Id, EstadoMensagem.Enviada),
            (r.DestinatarioId!.Value, r.FioId, r.AlunoId!.Value, r.Estado));
        Assert.Equal("mensagem.responder", _auditoria.Registros[^1].Acao);
    }

    [Fact]
    public async Task Resposta_apos_transferencia_nao_chega_ao_coordenador_antigo()
    {
        var prof = Eu();
        var coordenador = Eu(Perfis.CoordenadorCurso, _curso);
        var original = await Enviar(prof).Handle(Mensagem(coordenador.Id, _aluno.Id), Ct);
        _aluno.TransferirCurso(_outroCurso, Hoje, "Mudança de curso", Guid.NewGuid(), DateTimeOffset.UtcNow);

        await CampoInvalido("destinatarioId", () => Responder(prof).Handle(new(original, "Ainda por aqui?"), Ct));

        // O coordenador antigo ainda responde à Equipe NAP; a resposta grava o curso atual do aluno.
        var id = await Responder(coordenador).Handle(new(original, "Encerrando"), Ct);
        var resposta = _repo.Mensagens.Single(m => m.Id == id);
        Assert.Equal((prof.Id, _outroCurso), (resposta.DestinatarioId!.Value, resposta.CursoId!.Value));
        Assert.Equal(2, _repo.Mensagens.Count);
    }

    [Fact]
    public async Task Responder_rascunho_e_422_antes_de_validar_o_destinatario()
    {
        var eu = Eu();
        var destinatario = Usuario(Perfis.CoordenadorNap);
        var id = await CriarRascunho(eu).Handle(Rascunho(destinatario), Ct);
        Inativar(destinatario);

        await Assert.ThrowsAsync<DomainException>(() => Responder(eu).Handle(new(id, "Resposta"), Ct));
    }

    [Fact]
    public async Task Destinatario_inativo_e_rejeitado()
    {
        var autor = Eu();
        var inativo = Usuario(Perfis.CoordenadorNap, ativo: false);
        await CampoInvalido("destinatarioId", () => Enviar(autor).Handle(Mensagem(inativo), Ct));

        // Na resposta, o outro participante é revalidado: se ficou inativo, ninguém recebe.
        var destinatario = Eu(Perfis.CoordenadorNap);
        var original = await EnviadaAsync(autor, destinatario.Id);
        Inativar(autor.Id);
        await CampoInvalido("destinatarioId", () => Responder(destinatario).Handle(new(original, "Resposta"), Ct));
        Assert.Single(_repo.Mensagens);
    }

    [Fact]
    public async Task Gestao_recebe_403()
    {
        var autor = Eu();
        var id = await EnviadaAsync(autor, Usuario(Perfis.CoordenadorNap));
        var gestao = Eu(Perfis.Gestao);
        var consultas = new ComunicacaoQueriesNaoUsada();
        var acoes = new Func<Task>[]
        {
            () => Enviar(gestao).Handle(Mensagem(autor.Id), Ct),
            () => CriarRascunho(gestao).Handle(Rascunho(null), Ct),
            () => AtualizarRascunho(gestao).Handle(new(id, TipoMensagem.Aviso, "Assunto", "Texto", null, null, null, null, "v1"), Ct),
            () => DescartarRascunho(gestao).Handle(new(id), Ct),
            () => Responder(gestao).Handle(new(id, "Resposta"), Ct),
            () => MarcarLida(gestao).Handle(new(id), Ct),
            () => Arquivar(gestao).Handle(new(id), Ct),
            () => Desarquivar(gestao).Handle(new(id), Ct),
            () => Destinatarios(gestao).Handle(new(), Ct),
            () => new ListarCaixaHandler(new ListarCaixaValidator(), consultas, gestao).Handle(new(), Ct),
            () => new ObterMensagemHandler(new ObterMensagemValidator(), consultas, gestao).Handle(new(id), Ct),
            () => new ContarMensagensNaoLidasHandler(consultas, gestao).Handle(Ct),
        };

        foreach (var acao in acoes)
            await Assert.ThrowsAsync<ForbiddenException>(acao);
        Assert.Single(_repo.Mensagens);
    }

    [Fact]
    public async Task Envio_gera_evento_e_notificacao_ao_destinatario()
    {
        var autor = Eu(nome: "Ana Beatriz");
        var destinatario = Usuario(Perfis.CoordenadorCurso, _curso);
        var notificacoes = new NotificacaoRepositoryEmMemoria();
        var email = new EnvioEmailFalso();

        var id = await Enviar(autor).Handle(Mensagem(destinatario, _aluno.Id, tipo: TipoMensagem.SolicitacaoInformacao), Ct);
        var evento = Assert.IsType<MensagemEnviada>(Assert.Single(_repo.Mensagens[0].Eventos));
        var criadas = await new GerarNotificacoesHandler(notificacoes, _usuarios, _alunos, _repo, email, new RelogioFixo(), _uow)
            .Handle(evento, Ct);

        Assert.Equal((id, autor.Id, destinatario), (evento.MensagemId, evento.RemetenteId, evento.DestinatarioId));
        Assert.Equal(1, criadas);
        var n = Assert.Single(notificacoes.Notificacoes);
        Assert.Equal(destinatario, n.DestinatarioId);
        Assert.Equal("Nova mensagem: Assunto da mensagem", n.Titulo);
        Assert.Equal("Ana Beatriz · Solicitação de informação", n.Texto);
        Assert.Equal($"/comunicacoes/{id}", n.Link);
        Assert.Equal(destinatario, Assert.Single(email.Enviados).DestinatarioId);
    }

    [Fact]
    public async Task Destinatarios_disponiveis_seguem_a_mesma_regra_do_envio()
    {
        var eu = Eu();
        foreach (var perfil in Perfis.Todos)
        {
            Usuario(perfil, perfil == Perfis.CoordenadorCurso ? _curso : null);
            Usuario(perfil, perfil == Perfis.CoordenadorCurso ? _outroCurso : null);
        }
        Usuario(Perfis.ProfissionalNap, ativo: false);
        var cenarios = new (Guid? AlunoId, Guid? EncaminhamentoId)[]
        {
            (null, null), (_aluno.Id, null), (_aluno.Id, Encaminhar(_aluno, DestinoEncaminhamento.Sip).Id),
            (null, Encaminhar(_aluno, DestinoEncaminhamento.Externo).Id),
        };

        foreach (var (alunoId, encaminhamentoId) in cenarios)
        {
            var listados = (await Destinatarios(eu).Handle(new(alunoId, encaminhamentoId), Ct)).Select(d => d.Id).ToHashSet();
            foreach (var (id, _, _, _) in _usuarios.Usuarios)
            {
                var aceito = true;
                try { await Enviar(eu).Handle(Mensagem(id, alunoId, encaminhamentoId), Ct); }
                catch (ValidationException) { aceito = false; }
                Assert.True(aceito == listados.Contains(id), $"{alunoId} {encaminhamentoId} {id}");
            }
        }

        var semVinculo = await Destinatarios(eu).Handle(new(), Ct);
        Assert.DoesNotContain(semVinculo, d => d.Id == eu.Id || d.Perfil == Perfis.Gestao);
        Assert.Equal(semVinculo.OrderBy(d => d.Nome, StringComparer.Ordinal).Select(d => d.Id), semVinculo.Select(d => d.Id));
        var comSip = await Destinatarios(eu).Handle(new(cenarios[2].AlunoId, cenarios[2].EncaminhamentoId), Ct);
        Assert.Contains(comSip, d => d.Perfil == Perfis.Sip);
        Assert.Single(comSip, d => d.Perfil == Perfis.CoordenadorCurso);
    }

    [Fact]
    public async Task Destinatarios_com_vinculo_invalido_e_400()
    {
        var sip = Eu(Perfis.Sip);
        await CampoInvalido("alunoId", () => Destinatarios(sip).Handle(new(_aluno.Id), Ct));
    }

    /// <summary>Só para os casos de Gestão, que são barrados antes de qualquer consulta.</summary>
    private sealed class ComunicacaoQueriesNaoUsada : IComunicacaoQueries
    {
        public Task<PagedResult<MensagemListaDto>> ListarAsync(Guid usuarioId, CaixaMensagens caixa, string? busca, Guid? alunoId,
            int pagina, int tamanho, CancellationToken ct) => throw new InvalidOperationException();
        public Task<MensagemDetalheDto?> ObterAsync(Guid id, Guid usuarioId, CancellationToken ct) => throw new InvalidOperationException();
        public Task<int> ContarNaoLidasAsync(Guid usuarioId, CancellationToken ct) => throw new InvalidOperationException();
    }
}
