using Nap.Domain.Atendimentos;

namespace Nap.Api.Contratos;

/// <summary>Justificativa obrigatória quando quem corrige não é o responsável.</summary>
public record CorrigirAtendimentoRequest(EstadoAtendimento Estado, TipoAtendimento Tipo, DateTimeOffset Inicio, DateTimeOffset Fim,
    string? DemandaPrincipal, string? ObservacoesPrivadas, string? RecomendacoesEncaminhamento, string? ProximosPassos,
    string? Justificativa);

/// <summary>Com <c>compromissoId</c>: registro do agendamento. Sem ele: avulso (aluno, início, fim e justificativa).</summary>
public record RegistrarAtendimentoRequest(Guid? CompromissoId, Guid? AlunoId, Guid? ProfissionalId, DateTimeOffset? Inicio,
    DateTimeOffset? Fim, TipoAtendimento Tipo, EstadoAtendimento Estado, string? DemandaPrincipal, string? ObservacoesPrivadas,
    string? RecomendacoesEncaminhamento, string? ProximosPassos, string? JustificativaAvulso);
