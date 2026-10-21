namespace Nap.Infrastructure.Persistencia;

/// <summary>Linha de auditoria, só inserida (append-only). Nunca guarda conteúdo clínico.</summary>
public class EventoAuditoria
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid? UsuarioId { get; private set; }
    public string Acao { get; private set; } = null!;
    public string Recurso { get; private set; } = null!;
    public Guid? RecursoId { get; private set; }
    public string Resultado { get; private set; } = null!;
    public DateTimeOffset Em { get; private set; }

    private EventoAuditoria() { } // ORM

    public EventoAuditoria(Guid? usuarioId, string acao, string recurso, Guid? recursoId, string resultado, DateTimeOffset em)
    {
        UsuarioId = usuarioId;
        Acao = acao;
        Recurso = recurso;
        RecursoId = recursoId;
        Resultado = resultado;
        Em = em;
    }
}
