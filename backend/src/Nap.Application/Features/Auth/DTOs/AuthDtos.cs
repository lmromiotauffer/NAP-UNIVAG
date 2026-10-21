namespace Nap.Application.Features.Auth;

public record UsuarioSessaoDto(Guid Id, string Nome, string Email, string Perfil, Guid? CursoId, string? Curso, bool DeveTrocarSenha);

/// <summary>Par de tokens emitido no login ou na renovação; o refresh token só existe aqui (o banco guarda o hash).</summary>
public record SessaoEmitida(string AccessToken, DateTimeOffset ExpiraEm, string RefreshToken, DateTimeOffset RefreshExpiraEm,
    UsuarioSessaoDto Usuario);
