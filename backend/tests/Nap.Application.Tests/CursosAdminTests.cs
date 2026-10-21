using Nap.Application.Comum;
using Nap.Application.Features.Alunos.Commands;
using Nap.Application.Features.Cursos.Commands;
using Nap.Application.Features.Usuarios.Commands;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Alunos;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Cursos;

namespace Nap.Application.Tests;

public class CursosAdminTests
{
    private readonly CursoRepositoryEmMemoria _cursos = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new();
    private readonly CacheConsultasFalso _cache = new();
    private readonly UsuarioAtualFalso _coord = new(Perfis.CoordenadorNap);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private CriarCursoHandler Criar(IUsuarioAtual? u = null) => new(new CriarCursoValidator(), _cursos, u ?? _coord, _auditoria, _uow, _cache);

    private RenomearCursoHandler Renomear() => new(new RenomearCursoValidator(), _cursos, _coord, _auditoria, _uow, _cache);

    private AlterarSituacaoCursoHandler Situacao() => new(new AlterarSituacaoCursoValidator(), _cursos, _coord, _auditoria, _uow, _cache);

    /// <summary>Consultas de curso ativo refletindo o estado atual do repositório.</summary>
    private CursoQueriesFalso Ativos() => new(_cursos.Cursos.Where(c => c.Ativo).Select(c => c.Id).ToArray());

    [Fact]
    public async Task Curso_com_nome_repetido_retorna_conflito()
    {
        var id = await Criar().Handle(new CriarCursoCommand(" Direito "), Ct);
        Assert.Equal("Direito", Assert.Single(_cursos.Cursos).Nome);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Criar().Handle(new CriarCursoCommand("DIREITO"), Ct));
        Assert.Equal(RegrasCurso.MensagemNomeRepetido, ex.Message);

        var outro = await Criar().Handle(new CriarCursoCommand("Psicologia"), Ct);
        await Assert.ThrowsAsync<ConflictException>(() => Renomear().Handle(new RenomearCursoCommand(outro, "direito", "v"), Ct));
        // Renomear para o próprio nome (mudando a caixa) não conflita consigo mesmo.
        await Renomear().Handle(new RenomearCursoCommand(id, "DIREITO", "v"), Ct);
        Assert.Equal("DIREITO", _cursos.Cursos.Single(c => c.Id == id).Nome);
        Assert.Contains(("curso.renomear", "Curso", (Guid?)id), _auditoria.Registros);
        Assert.Equal(["v", "v"], _uow.VersoesExigidas);
    }

    [Fact]
    public async Task Curso_inativo_nao_recebe_aluno_nem_coordenador()
    {
        var id = await Criar().Handle(new CriarCursoCommand("Direito"), Ct);
        await Situacao().Handle(new AlterarSituacaoCursoCommand(id, false, "v"), Ct);
        Assert.False(_cursos.Cursos.Single().Ativo);
        Assert.Contains(("curso.inativar", "Curso", (Guid?)id), _auditoria.Registros);

        var profissional = Guid.NewGuid();
        var criarAluno = new CriarAlunoHandler(new CriarAlunoValidator(), new AlunoRepositoryEmMemoria(), new UsuarioQueriesFalso(profissional),
            Ativos(), _coord, new RelogioFixo(), _auditoria, _uow, _cache);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => criarAluno.Handle(new CriarAlunoCommand("Maria da Silva", "2024001",
            id, 3, new DateOnly(2026, 8, 1), profissional, null, null), Ct));
        Assert.Contains("cursoId", ex.Erros.Keys);

        var alunos = new AlunoRepositoryEmMemoria();
        var aluno = Aluno.Criar("Maria da Silva", Matricula.Criar("2024001"), Guid.NewGuid(), 3, new DateOnly(2026, 8, 1),
            profissional, null, null, profissional, DateTimeOffset.UtcNow);
        alunos.Adicionar(aluno);
        ex = await Assert.ThrowsAsync<ValidationException>(() => new TransferirCursoAlunoHandler(new TransferirCursoAlunoValidator(), alunos,
            Ativos(), _coord, new RelogioFixo(), _auditoria, _uow, _cache).Handle(new TransferirCursoAlunoCommand(aluno.Id, id,
            new DateOnly(2026, 9, 1), "Mudou de curso", "v"), Ct));
        Assert.Contains("novoCursoId", ex.Erros.Keys);

        var gestao = new GestaoUsuariosFalsa(_auditoria);
        ex = await Assert.ThrowsAsync<ValidationException>(() => new CriarUsuarioHandler(new CriarUsuarioValidator(), gestao, Ativos(),
            _coord, _auditoria, _cache).Handle(new CriarUsuarioCommand("coord@teste.nap", "Coordenadora", Perfis.CoordenadorCurso, id), Ct));
        Assert.Contains("cursoId", ex.Erros.Keys);

        // Reativado, volta a receber.
        await Situacao().Handle(new AlterarSituacaoCursoCommand(id, true, "v"), Ct);
        Assert.Contains(("curso.reativar", "Curso", (Guid?)id), _auditoria.Registros);
        await new CriarUsuarioHandler(new CriarUsuarioValidator(), gestao, Ativos(), _coord, _auditoria, _cache)
            .Handle(new CriarUsuarioCommand("coord@teste.nap", "Coordenadora", Perfis.CoordenadorCurso, id), Ct);
    }

    [Fact]
    public async Task Administracao_de_cursos_exige_coordenacao()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => Criar(new UsuarioAtualFalso(Perfis.CoordenadorCurso)).Handle(new CriarCursoCommand("Direito"), Ct));
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Criar().Handle(new CriarCursoCommand("x"), Ct));
        Assert.Contains("nome", ex.Erros.Keys);
        await Assert.ThrowsAsync<NotFoundException>(() => Situacao().Handle(new AlterarSituacaoCursoCommand(Guid.NewGuid(), false, "v"), Ct));
        Assert.Empty(_cursos.Cursos);
    }

    [Fact]
    public async Task Situacao_inalterada_nao_grava()
    {
        var curso = Curso.Criar("Direito");
        _cursos.Adicionar(curso);
        await Situacao().Handle(new AlterarSituacaoCursoCommand(curso.Id, true, "v"), Ct);
        Assert.Equal(0, _uow.Salvamentos);
        Assert.Empty(_auditoria.Registros);
    }
}
