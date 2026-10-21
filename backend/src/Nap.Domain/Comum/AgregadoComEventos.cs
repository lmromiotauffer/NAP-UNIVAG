namespace Nap.Domain.Comum;

public abstract class AgregadoComEventos : Entidade
{
    private readonly List<IEventoDominio> _eventos = [];

    public IReadOnlyCollection<IEventoDominio> Eventos => _eventos;

    public void LimparEventos() => _eventos.Clear();

    protected void Registrar(IEventoDominio evento) => _eventos.Add(evento);
}
