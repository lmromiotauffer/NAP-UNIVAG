using Nap.Application.Features.Notificacoes;
using Nap.Domain.Notificacoes;

namespace Nap.Application.Tests.Fakes;

public sealed class NotificacaoRepositoryEmMemoria : INotificacaoRepository
{
    public List<Notificacao> Notificacoes { get; } = [];

    public Task<IReadOnlyList<Guid>> DestinatariosJaNotificadosAsync(Guid eventoId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(Notificacoes.Where(n => n.EventoId == eventoId).Select(n => n.DestinatarioId).ToList());

    public Task<Notificacao?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Notificacoes.FirstOrDefault(n => n.Id == id));

    public Task<IReadOnlyList<Notificacao>> ListarNaoLidasAsync(Guid destinatarioId, string tipo, string link, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Notificacao>>(Notificacoes
            .Where(n => n.DestinatarioId == destinatarioId && n.Tipo == tipo && n.Link == link && n.LidaEm == null).ToList());

    public Task MarcarTodasLidasAsync(Guid destinatarioId, DateTimeOffset agora, CancellationToken ct)
    {
        foreach (var n in Notificacoes.Where(n => n.DestinatarioId == destinatarioId))
            n.MarcarLida(agora);
        return Task.CompletedTask;
    }

    public void Adicionar(Notificacao n) => Notificacoes.Add(n);
}
