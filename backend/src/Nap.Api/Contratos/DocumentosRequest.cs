using Nap.Domain.Documentos;

namespace Nap.Api.Contratos;

public record AlterarCategoriaDocumentoRequest(CategoriaDocumento Categoria);

public record JustificativaDocumentoRequest(string Justificativa);

public record ArquivarDocumentoRequest(string Motivo);
