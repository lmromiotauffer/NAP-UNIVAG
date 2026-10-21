using Nap.Application.Features.Auth;

namespace Nap.Api.Contratos;

public record LoginRequest(string Email, string Senha);

public record TrocarSenhaRequest(string SenhaAtual, string NovaSenha);

/// <summary>Resposta de login/renovação. O refresh token vai só no cookie <c>nap_refresh</c>, nunca no corpo.</summary>
public record SessaoDto(string AccessToken, UsuarioSessaoDto Usuario);

/// <summary>Corpo das respostas 201 de criação.</summary>
public record IdCriado(Guid Id);
