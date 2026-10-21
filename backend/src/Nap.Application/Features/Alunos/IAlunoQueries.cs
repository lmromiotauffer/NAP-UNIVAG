using Nap.Application.Comum;
using Nap.Domain.Alunos;
using Nap.Domain.Comum;
using Nap.Domain.Encaminhamentos;

namespace Nap.Application.Features.Alunos;

public record EscopoAlunos(Guid? SomenteCursoId, bool IncluirClinico)
{
    /// <summary>Equipe NAP vê tudo; coordenador de curso só alunos que passaram pelo próprio curso (curso da época, como nas demais
    /// consultas), sem dados clínicos; demais perfis são negados.</summary>
    public static EscopoAlunos Para(IUsuarioAtual usuario) =>
        usuario.EhEquipeNap() ? new(null, true)
        : usuario.Perfil == Perfis.CoordenadorCurso ? new(usuario.CursoId ?? throw new ForbiddenException(), false)
        : throw new ForbiddenException();
}

public record FiltroAlunos(string? Busca, Guid? CursoId, Guid? ProfissionalId, SituacaoAluno? Situacao,
    DateOnly? InicioDe, DateOnly? InicioAte, int Pagina, int Tamanho,
    Especialidade? EspecialidadeEncaminhamento = null);

public interface IAlunoQueries
{
    Task<PagedResult<AlunoListaDto>> ListarAsync(FiltroAlunos f, EscopoAlunos e, CancellationToken ct);
    Task<AlunoFichaDto?> ObterFichaAsync(Guid id, EscopoAlunos e, CancellationToken ct);
    /// <summary><c>null</c> quando o aluno não existe ou está fora do escopo (mesma regra da ficha).</summary>
    Task<PagedResult<EventoLinhaDoTempoDto>?> LinhaDoTempoAsync(Guid alunoId, EscopoAlunos e, int pagina, int tamanho, CancellationToken ct);
}
