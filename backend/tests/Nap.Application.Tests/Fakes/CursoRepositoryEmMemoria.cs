using Nap.Application.Features.Cursos;
using Nap.Domain.Cursos;

namespace Nap.Application.Tests.Fakes;

public sealed class CursoRepositoryEmMemoria : ICursoRepository
{
    public List<Curso> Cursos { get; } = [];

    public Task<Curso?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Cursos.FirstOrDefault(c => c.Id == id));

    public Task<bool> NomeExisteAsync(string nome, Guid? ignorarId, CancellationToken ct) =>
        Task.FromResult(Cursos.Any(c => c.Id != ignorarId && string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase)));

    public void Adicionar(Curso c) => Cursos.Add(c);
}
