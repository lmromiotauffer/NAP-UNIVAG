using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Alunos;
using Nap.Domain.Alunos;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class AlunoRepository(NapDbContext db) : IAlunoRepository
{
    public Task<Aluno?> ObterAsync(Guid id, CancellationToken ct) =>
        db.Alunos.Include(a => a.HistoricoSituacao).Include(a => a.HistoricoCursos).AsSplitQuery()
            .SingleOrDefaultAsync(a => a.Id == id, ct);

    public Task<bool> MatriculaExisteAsync(string matricula, CancellationToken ct) =>
        db.Alunos.AnyAsync(a => a.Matricula.Valor == matricula, ct);

    public void Adicionar(Aluno a) => db.Alunos.Add(a);
}
