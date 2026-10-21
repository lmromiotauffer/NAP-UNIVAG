using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Atividades;

namespace Nap.Application.Features.Agenda;

/// <param name="Participantes">Só no compromisso da equipe (<c>GrupoId</c>): quem tem instância Agendada ou Concluída no grupo, por nome.</param>
/// <param name="PodeGerirGrupo">O usuário atual pode remarcar/editar/cancelar o grupo todo (<c>escopo=Grupo</c>): coordenação ou criador do grupo. Só na agenda da semana.</param>
public record CompromissoDto(Guid Id, TipoCompromisso Tipo, string Titulo, DateTimeOffset Inicio, DateTimeOffset Fim,
    Guid ProfissionalId, string Profissional, Guid? AlunoId, string? Aluno, EstadoCompromisso Estado,
    EstadoAtendimento? EstadoAtendimento, Guid? RemarcadoDeId, string? Descricao, string Versao, Guid? GrupoId,
    IReadOnlyList<ParticipanteDto>? Participantes, bool PodeGerirGrupo = false);

public record ParticipanteDto(Guid Id, string Nome);

/// <summary>Atendimento avulso (sem compromisso) que ocupa horário; sem dados clínicos.</summary>
public record AtendimentoAvulsoAgendaDto(Guid Id, DateTimeOffset Inicio, DateTimeOffset Fim, Guid ProfissionalId, string Profissional,
    Guid AlunoId, string Aluno, TipoAtendimento Tipo, EstadoAtendimento Estado);

/// <summary>Atividade avulsa (sem compromisso) que ocupa horário; sem descrição.</summary>
public record AtividadeAvulsaAgendaDto(Guid Id, DateTimeOffset Inicio, DateTimeOffset Fim, Guid ProfissionalId, string Profissional,
    TipoAtividade Tipo);

public record AgendaSemanaDto(DateOnly Inicio, DateOnly Fim, IReadOnlyList<CompromissoDto> Compromissos,
    IReadOnlyList<AtendimentoAvulsoAgendaDto> AtendimentosAvulsos, IReadOnlyList<AtividadeAvulsaAgendaDto> AtividadesAvulsas);

public record FaixaEscalaDto(DayOfWeek Dia, TimeOnly Inicio, TimeOnly Fim, bool SemAtendimento);
