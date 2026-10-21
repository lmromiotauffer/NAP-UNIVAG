using Nap.Application.Comum;
using Nap.Domain.Notificacoes;

namespace Nap.Infrastructure.Notificacoes;

/// <summary>Sem provedor de e-mail configurado: nada é enviado e a notificação fica com o e-mail pendente.</summary>
public sealed class EnvioEmailNaoConfigurado : IEnvioEmail
{
    public Task<EstadoEmail> EnviarAsync(Guid destinatarioId, string assunto, string corpo, CancellationToken ct) =>
        Task.FromResult(EstadoEmail.NaoConfigurado);
}
