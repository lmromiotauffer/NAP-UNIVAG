using Nap.Domain.Atendimentos;

namespace Nap.Application.Features.Atendimentos;

/// <summary>Item de lista: nunca inclui observações privadas.</summary>
public record AtendimentoListaDto(Guid Id, DateTimeOffset Inicio, int DuracaoMinutos, Guid AlunoId, string Aluno,
    TipoAtendimento Tipo, string? DemandaPrincipal, Guid ProfissionalId, string Profissional, EstadoAtendimento Estado, bool Avulso);

public record DadosClinicosDto(string? DemandaPrincipal, string? ObservacoesPrivadas, string? RecomendacoesEncaminhamento,
    string? ProximosPassos);

public record CorrecaoAtendimentoDto(string Autor, DateTimeOffset Em, string? Justificativa,
    EstadoAtendimento EstadoAnterior, EstadoAtendimento EstadoNovo, TipoAtendimento TipoAnterior,
    DateTimeOffset InicioAnterior, DateTimeOffset FimAnterior);

public record AtendimentoDetalheDto(Guid Id, Guid? CompromissoId, DateTimeOffset Inicio, DateTimeOffset Fim, int DuracaoMinutos,
    Guid AlunoId, string Aluno, Guid ProfissionalId, string Profissional, TipoAtendimento Tipo, EstadoAtendimento Estado,
    bool Avulso, string? JustificativaAvulso, DadosClinicosDto? DadosClinicos, IReadOnlyList<CorrecaoAtendimentoDto> Correcoes,
    string Versao);
