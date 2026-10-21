using Nap.Application.Comum;

namespace Nap.Application.Features.Notificacoes;

public interface INotificacaoQueries
{
    Task<PagedResult<NotificacaoDto>> ListarAsync(Guid destinatarioId, bool naoLidas, int pagina, int tamanho, CancellationToken ct);
    Task<int> ContarNaoLidasAsync(Guid destinatarioId, CancellationToken ct);
}
