using Nap.Domain.Encaminhamentos;

namespace Nap.Application.Features.Encaminhamentos;

/// <summary>Item de lista: nunca inclui motivo restrito nem observações. Especialidade e destino chegam <c>null</c> ao
/// coordenador de curso quando o destino não é a própria coordenação.</summary>
public record EncaminhamentoListaDto(Guid Id, Guid AlunoId, string Aluno, string Matricula, string Curso,
    Especialidade? Especialidade, DestinoEncaminhamento? Destino, EstadoEncaminhamento Estado, DateOnly Data, string Solicitante,
    bool SemAcolhimentoNoPrazo, DateTimeOffset AtualizadoEm);

public record EvolucaoEncaminhamentoDto(DateTimeOffset Em, string Autor, AcaoRetorno Acao, EstadoEncaminhamento EstadoNovo,
    DateOnly? Data, string? Texto);

/// <summary>Campos restritos chegam <c>null</c> quando o escopo não permite vê-los.</summary>
public record EncaminhamentoDetalheDto(Guid Id, Guid AlunoId, string Aluno, string Matricula, string Curso,
    Especialidade? Especialidade, DestinoEncaminhamento? Destino, EstadoEncaminhamento Estado, DateOnly Data, string Solicitante,
    bool SemAcolhimentoNoPrazo, DateTimeOffset AtualizadoEm, Guid SolicitanteId, string? MotivoRestrito,
    string? ResumoCompartilhavel, string? Observacoes, DateOnly? DataAcolhimento, DateOnly? DataPrimeiroAtendimento,
    DateOnly? DataEncerramento, bool PodeRegistrarRetorno, bool PodeCancelar, IReadOnlyList<EvolucaoEncaminhamentoDto> Evolucoes,
    string Versao);
