using Nap.Domain.Alunos;

namespace Nap.Application.Features.Alunos;

public interface IAlunoRepository
{
    Task<Aluno?> ObterAsync(Guid id, CancellationToken ct);
    Task<bool> MatriculaExisteAsync(string matricula, CancellationToken ct);
    void Adicionar(Aluno a);
}
