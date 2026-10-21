using Nap.Application.Comum;

namespace Nap.Application.Features.Peis;

public interface IPeiQueries
{
    /// <summary><c>null</c> quando o aluno não existe ou está fora do escopo.</summary>
    Task<IReadOnlyList<PeiResumoDto>?> ListarDoAlunoAsync(Guid alunoId, EscopoPeis e, Guid usuarioId, CancellationToken ct);

    /// <summary><c>null</c> quando a versão não existe ou está fora do escopo.</summary>
    Task<VersaoPeiDetalheDto?> ObterVersaoAsync(Guid versaoId, EscopoPeis e, IUsuarioAtual u, CancellationToken ct);
}
