namespace Nap.Application.Features.Usuarios;

/// <summary><c>Versao</c> é o <c>ConcurrencyStamp</c> do Identity (vai no ETag/If-Match).</summary>
public record UsuarioAdminDto(Guid Id, string Nome, string Email, string Perfil, Guid? CursoId, string? Curso, bool Ativo,
    bool DeveTrocarSenha, string Versao);

public record FiltroUsuarios(string? Busca, string? Perfil, Guid? CursoId, bool? Ativo, int Pagina, int Tamanho);

/// <summary>O id vem do handler para a auditoria ser registrada antes da criação.</summary>
public record NovoUsuario(Guid Id, string Email, string NomeCompleto, string Perfil, Guid? CursoId);

/// <summary>Exibida uma única vez; nunca logada nem auditada.</summary>
public record SenhaTemporariaDto(Guid UsuarioId, string SenhaTemporaria);
