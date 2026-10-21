using Nap.Domain.Atividades;

namespace Nap.Api.Contratos;

/// <summary>Toda correção exige justificativa; a versão vem do cabeçalho <c>If-Match</c>.</summary>
public record CorrigirAtividadeRequest(DateTimeOffset Inicio, DateTimeOffset Fim, Guid? CursoId, TipoAtividade Tipo, string Descricao,
    string Justificativa);

/// <summary>Com <c>compromissoId</c>: registro do agendamento. Sem ele: avulsa (exige justificativa).</summary>
public record RegistrarAtividadeRequest(Guid? CompromissoId, Guid? ProfissionalId, DateTimeOffset Inicio, DateTimeOffset Fim,
    Guid? CursoId, TipoAtividade Tipo, string Descricao, string? JustificativaAvulso);
