using Nap.Domain.Comum;

namespace Nap.Domain.Alunos;

public class VinculoCurso : Entidade
{
    public Guid CursoId { get; private set; }
    public DateOnly Desde { get; private set; }
    public DateOnly? Ate { get; private set; }
    public string Motivo { get; private set; } = null!;
    public Guid AutorId { get; private set; }
    public DateTimeOffset RegistradoEm { get; private set; }

    private VinculoCurso() { } // ORM

    internal static VinculoCurso Criar(Guid cursoId, DateOnly desde, string motivo, Guid autorId, DateTimeOffset registradoEm) =>
        new() { CursoId = cursoId, Desde = desde, Motivo = motivo, AutorId = autorId, RegistradoEm = registradoEm };

    internal void Encerrar(DateOnly ate) => Ate = ate;
}
