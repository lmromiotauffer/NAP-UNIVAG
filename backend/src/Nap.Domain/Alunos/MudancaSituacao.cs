using Nap.Domain.Comum;

namespace Nap.Domain.Alunos;

public class MudancaSituacao : Entidade
{
    public SituacaoAluno? De { get; private set; }
    public SituacaoAluno Para { get; private set; }
    public string Motivo { get; private set; } = null!;
    public Guid AutorId { get; private set; }
    public DateTimeOffset Em { get; private set; }

    private MudancaSituacao() { } // ORM

    internal static MudancaSituacao Criar(SituacaoAluno? de, SituacaoAluno para, string motivo, Guid autorId, DateTimeOffset em) =>
        new() { De = de, Para = para, Motivo = motivo, AutorId = autorId, Em = em };
}
