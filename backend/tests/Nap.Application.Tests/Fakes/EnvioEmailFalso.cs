using Nap.Application.Comum;
using Nap.Domain.Notificacoes;

namespace Nap.Application.Tests.Fakes;

public sealed class EnvioEmailFalso(bool falhar = false) : IEnvioEmail
{
    public List<(Guid DestinatarioId, string Assunto, string Corpo)> Enviados { get; } = [];

    public Task<EstadoEmail> EnviarAsync(Guid destinatarioId, string assunto, string corpo, CancellationToken ct)
    {
        if (falhar) throw new InvalidOperationException("SMTP fora do ar");
        Enviados.Add((destinatarioId, assunto, corpo));
        return Task.FromResult(EstadoEmail.Enviado);
    }
}
