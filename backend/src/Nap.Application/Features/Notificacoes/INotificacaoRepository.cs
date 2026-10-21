using Nap.Domain.Notificacoes;

namespace Nap.Application.Features.Notificacoes;

public interface INotificacaoRepository
{
    Task<IReadOnlyList<Guid>> DestinatariosJaNotificadosAsync(Guid eventoId, CancellationToken ct);
    Task<Notificacao?> ObterAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Notificacao>> ListarNaoLidasAsync(Guid destinatarioId, string tipo, string link, CancellationToken ct);
    /// <summary>Atualiza direto no banco, fora da unidade de trabalho.</summary>
    Task MarcarTodasLidasAsync(Guid destinatarioId, DateTimeOffset agora, CancellationToken ct);
    void Adicionar(Notificacao n);
}
