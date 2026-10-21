using Nap.Domain.Comum;

namespace Nap.Domain.Notificacoes;

public class Notificacao : Entidade
{
    public Guid DestinatarioId { get; private set; }
    public Guid EventoId { get; private set; }
    public string Tipo { get; private set; } = null!;
    public string Titulo { get; private set; } = null!;
    public string Texto { get; private set; } = null!;
    public string Link { get; private set; } = null!;
    public DateTimeOffset CriadaEm { get; private set; }
    public DateTimeOffset? LidaEm { get; private set; }
    public EstadoEmail EstadoEmail { get; private set; }

    private Notificacao() { } // ORM

    public static Notificacao Criar(Guid destinatarioId, Guid eventoId, string tipo, string titulo, string texto, string link,
        DateTimeOffset agora) =>
        new()
        {
            DestinatarioId = destinatarioId,
            EventoId = eventoId,
            Tipo = Validar(tipo, 60, "O tipo"),
            Titulo = Validar(titulo, 200, "O título"),
            Texto = Validar(texto, 1000, "O texto"),
            Link = Validar(link, 200, "O link"),
            CriadaEm = agora,
            EstadoEmail = EstadoEmail.NaoConfigurado,
        };

    public void MarcarLida(DateTimeOffset agora) => LidaEm ??= agora;

    public void DefinirEstadoEmail(EstadoEmail e) => EstadoEmail = e;

    private static string Validar(string texto, int maximo, string nome)
    {
        texto = (texto ?? string.Empty).Trim();
        if (texto.Length is 0 || texto.Length > maximo)
            throw new DomainException($"{nome} da notificação deve ter de 1 a {maximo} caracteres.");
        return texto;
    }
}
