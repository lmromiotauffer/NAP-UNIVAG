using Nap.Application.Features.Peis.Commands;

namespace Nap.Api.Contratos;

public record CriarPeiRequest(Guid ProfissionalResponsavelId, ConteudoPeiInput Conteudo);

public record DevolverPeiRequest(string Justificativa);

public record TrocarResponsavelPeiRequest(Guid NovoResponsavelId, string Justificativa);

public record ArquivarPeiRequest(string Motivo);

/// <summary>Token no corpo, nunca no caminho nem na query: não vai para logs de acesso, histórico ou Referer.</summary>
public record AbrirAssinaturaRequest(string Token, string Codigo);

public record AssinarPeiAlunoRequest(string Token, string Codigo, string NomeCompleto, bool Aceite);
