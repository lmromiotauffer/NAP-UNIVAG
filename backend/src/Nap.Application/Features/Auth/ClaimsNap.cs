namespace Nap.Application.Features.Auth;

/// <summary>Claims do access token lidos fora da Infrastructure.</summary>
public static class ClaimsNap
{
    /// <summary>Presente ("1") enquanto o usuário precisa definir a própria senha; a Api bloqueia o resto até a troca.</summary>
    public const string TrocarSenha = "trocar_senha";
}
