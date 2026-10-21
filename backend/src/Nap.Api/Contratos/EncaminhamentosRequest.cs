using Nap.Domain.Encaminhamentos;

namespace Nap.Api.Contratos;

public record RegistrarRetornoRequest(AcaoRetorno Acao, DateOnly? Data, string? Texto);

public record CancelarEncaminhamentoRequest(string Justificativa);

public record CriarEncaminhamentoRequest(Guid AlunoId, Especialidade Especialidade, DestinoEncaminhamento Destino,
    DateOnly Data, string MotivoRestrito, string ResumoCompartilhavel, string? Observacoes);
