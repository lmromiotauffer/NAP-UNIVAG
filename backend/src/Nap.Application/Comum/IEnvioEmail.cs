using Nap.Domain.Notificacoes;

namespace Nap.Application.Comum;

public interface IEnvioEmail
{
    /// <summary>Envia ao e-mail do usuário e devolve o estado resultante; assunto e corpo nunca levam dados de aluno.</summary>
    Task<EstadoEmail> EnviarAsync(Guid destinatarioId, string assunto, string corpo, CancellationToken ct);
}
