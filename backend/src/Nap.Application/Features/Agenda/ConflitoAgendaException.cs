using Nap.Application.Comum;

namespace Nap.Application.Features.Agenda;

/// <summary>409 de bloqueio sobre horário ocupado: traz os itens em conflito para o usuário resolver.</summary>
public class ConflitoAgendaException(IReadOnlyList<ConflitoAgendaDto> conflitos) : ConflictException(MensagemPadrao)
{
    public const string MensagemPadrao = "Conflito de horário com outro compromisso, atendimento ou atividade do profissional ou do aluno";

    public IReadOnlyList<ConflitoAgendaDto> Conflitos { get; } = conflitos;
}
