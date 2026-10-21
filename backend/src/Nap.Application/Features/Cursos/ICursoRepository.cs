using Nap.Domain.Cursos;

namespace Nap.Application.Features.Cursos;

public interface ICursoRepository
{
    Task<Curso?> ObterAsync(Guid id, CancellationToken ct);
    /// <summary>Sem diferença de caixa.</summary>
    Task<bool> NomeExisteAsync(string nome, Guid? ignorarId, CancellationToken ct);
    void Adicionar(Curso c);
}
