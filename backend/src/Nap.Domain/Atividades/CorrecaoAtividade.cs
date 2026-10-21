using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Domain.Atividades;

public class CorrecaoAtividade : Entidade
{
    public Guid AutorId { get; private set; }
    public DateTimeOffset Em { get; private set; }
    public string Justificativa { get; private set; } = null!;
    public DateTimeOffset InicioAnterior { get; private set; }
    public DateTimeOffset FimAnterior { get; private set; }
    public Guid? CursoIdAnterior { get; private set; }
    public TipoAtividade TipoAnterior { get; private set; }
    public string DescricaoAnterior { get; private set; } = null!;

    private CorrecaoAtividade() { } // ORM

    internal static CorrecaoAtividade Criar(Guid autorId, DateTimeOffset em, string justificativa,
        IntervaloHorario intervaloAnterior, Guid? cursoIdAnterior, TipoAtividade tipoAnterior, string descricaoAnterior) =>
        new()
        {
            AutorId = autorId, Em = em, Justificativa = justificativa,
            InicioAnterior = intervaloAnterior.Inicio, FimAnterior = intervaloAnterior.Fim,
            CursoIdAnterior = cursoIdAnterior, TipoAnterior = tipoAnterior, DescricaoAnterior = descricaoAnterior,
        };
}
