namespace Nap.Application.Features.Auth;

/// <summary>
/// Sessão da Api: login, renovação rotativa, logout, usuário atual e troca da própria senha. Falhas de credencial ou de token
/// lançam <c>UnauthorizedException</c>; a implementação controla a própria transação.
/// </summary>
public interface IServicoSessao
{
    Task<SessaoEmitida> EntrarAsync(string email, string senha, CancellationToken ct);
    Task<SessaoEmitida> RenovarAsync(string refreshToken, CancellationToken ct);
    Task EncerrarAsync(string refreshToken, CancellationToken ct);
    Task<UsuarioSessaoDto> ObterUsuarioAsync(Guid usuarioId, CancellationToken ct);
    Task<SessaoEmitida> TrocarSenhaAsync(Guid usuarioId, string senhaAtual, string novaSenha, CancellationToken ct);
}
