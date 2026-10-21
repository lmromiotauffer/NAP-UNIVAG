using Nap.Domain.Comunicacoes;

namespace Nap.Application.Features.Comunicacoes;

public enum CaixaMensagens { Entrada, Enviados, Rascunhos, Arquivadas }

/// <summary><c>Data</c> = envio (ou última edição, no rascunho); <c>Recebida</c> = o usuário é o destinatário.</summary>
public record MensagemListaDto(Guid Id, TipoMensagem Tipo, string Assunto, string Remetente, string? Destinatario, Guid? AlunoId,
    string? Aluno, DateTimeOffset Data, bool Lida, bool Recebida);

public record MensagemFioDto(Guid Id, Guid RemetenteId, string Remetente, string Assunto, string Corpo, DateTimeOffset EnviadaEm);

/// <summary><c>Fio</c>: mensagens enviadas do mesmo fio em que o usuário participa, da mais antiga à mais recente.</summary>
public record MensagemDetalheDto(Guid Id, TipoMensagem Tipo, string Assunto, Guid RemetenteId, string Remetente,
    Guid? DestinatarioId, string? Destinatario, Guid? AlunoId, string? Aluno, DateTimeOffset Data, bool Lida, bool Recebida,
    string Corpo, Guid? EncaminhamentoId, Guid? CursoId, EstadoMensagem Estado, bool PodeResponder, bool PodeMarcarLida,
    bool Arquivada, IReadOnlyList<MensagemFioDto> Fio, string Versao);

public record DestinatarioDto(Guid Id, string Nome, string Perfil);
