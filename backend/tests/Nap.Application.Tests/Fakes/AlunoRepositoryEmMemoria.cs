using Nap.Application.Features.Alunos;
using Nap.Domain.Alunos;

namespace Nap.Application.Tests.Fakes;

public sealed class AlunoRepositoryEmMemoria : IAlunoRepository
{
    public List<Aluno> Alunos { get; } = [];

    public Task<Aluno?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Alunos.FirstOrDefault(a => a.Id == id));

    public Task<bool> MatriculaExisteAsync(string matricula, CancellationToken ct) =>
        Task.FromResult(Alunos.Any(a => a.Matricula.Valor == matricula));

    public void Adicionar(Aluno a) => Alunos.Add(a);
}
