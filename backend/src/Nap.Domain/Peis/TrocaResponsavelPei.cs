namespace Nap.Domain.Peis;

/// <summary>Histórico das trocas de responsável do PEI (com a justificativa, que não vai para a auditoria).</summary>
public sealed record TrocaResponsavelPei(Guid ResponsavelAnteriorId, Guid NovoResponsavelId, string Justificativa, Guid AutorId,
    DateTimeOffset Em);
