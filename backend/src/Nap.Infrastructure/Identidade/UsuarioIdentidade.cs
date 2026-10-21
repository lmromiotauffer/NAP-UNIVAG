using Microsoft.AspNetCore.Identity;

namespace Nap.Infrastructure.Identidade;

/// <summary>Usuário do sistema (ASP.NET Identity). <c>Perfil</c> vai no claim <c>role</c> do JWT e <c>Ativo = false</c> barra
/// login, renovação e a validação do carimbo de segurança.</summary>
public class UsuarioIdentidade : IdentityUser<Guid>
{
    public string NomeCompleto { get; set; } = null!;
    public string Perfil { get; set; } = null!;
    public Guid? CursoId { get; set; }
    public bool Ativo { get; set; }
    /// <summary>Data civil (fuso institucional) da última desativação; nula se ativo ou desativado antes deste campo existir.</summary>
    public DateOnly? DesativadoEm { get; set; }
    /// <summary>Data civil (fuso institucional) da criação; nula para usuários criados antes deste campo existir (sem limite).</summary>
    public DateOnly? AtivoDesde { get; set; }
    public bool DeveTrocarSenha { get; set; }
}
