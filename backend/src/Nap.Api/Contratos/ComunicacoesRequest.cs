using Nap.Domain.Comunicacoes;

namespace Nap.Api.Contratos;

/// <summary>Criação e edição de rascunho: o conteúdo ainda pode estar incompleto.</summary>
public record RascunhoRequest(TipoMensagem Tipo, string Assunto, string Corpo, Guid? DestinatarioId, Guid? CursoId, Guid? AlunoId,
    Guid? EncaminhamentoId);

public record EnviarMensagemRequest(Guid? RascunhoId, Guid DestinatarioId, TipoMensagem Tipo, string Assunto, string Corpo,
    Guid? CursoId, Guid? AlunoId, Guid? EncaminhamentoId);

public record ResponderMensagemRequest(string Corpo);
