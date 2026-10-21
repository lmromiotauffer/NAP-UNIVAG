using Nap.Domain.Comum;

namespace Nap.Domain.Comunicacoes;

public sealed record MensagemEnviada(Guid EventoId, DateTimeOffset OcorridoEm, Guid MensagemId, Guid RemetenteId,
    Guid DestinatarioId, TipoMensagem Tipo) : IEventoDominio
{
    internal MensagemEnviada(DateTimeOffset ocorridoEm, Guid mensagemId, Guid remetenteId, Guid destinatarioId,
        TipoMensagem tipo)
        : this(Guid.CreateVersion7(), ocorridoEm, mensagemId, remetenteId, destinatarioId, tipo) { }
}
