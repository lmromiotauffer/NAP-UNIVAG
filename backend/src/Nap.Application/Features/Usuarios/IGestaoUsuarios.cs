using Nap.Application.Comum;

namespace Nap.Application.Features.Usuarios;

/// <summary>
/// Porta de identidade da administração de usuários. Mudança de perfil, curso ou situação e redefinição de senha
/// rotacionam o carimbo de segurança e revogam todas as sessões do usuário. A auditoria registrada antes da chamada é
/// gravada junto. Versão antiga → <see cref="ConflictException"/>; e-mail repetido → <see cref="ConflictException"/>.
/// </summary>
public interface IGestaoUsuarios
{
    Task<UsuarioAdminDto?> ObterAsync(Guid id, CancellationToken ct);
    Task<PagedResult<UsuarioAdminDto>> ListarAsync(FiltroUsuarios f, CancellationToken ct);
    Task<int> ContarAdministradoresAtivosAsync(CancellationToken ct);
    Task<Guid> CriarAsync(NovoUsuario u, string senhaTemporaria, CancellationToken ct);
    Task AtualizarAsync(Guid id, string nome, string perfil, Guid? cursoId, string versao, CancellationToken ct);
    Task AlterarSituacaoAsync(Guid id, bool ativo, string versao, CancellationToken ct);
    Task RedefinirSenhaAsync(Guid id, string senhaTemporaria, string versao, CancellationToken ct);
}
