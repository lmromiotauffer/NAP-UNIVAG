using Nap.Domain.Comum;

namespace Nap.Application.Comum;

public interface IUsuarioAtual
{
    /// <summary>Id do usuário autenticado.</summary>
    /// <exception cref="UnauthorizedException">Quando não há usuário autenticado (ex.: operações do sistema).</exception>
    Guid Id { get; }
    string Perfil { get; }
    Guid? CursoId { get; }
}

public static class UsuarioAtualExtensoes
{
    public static bool EhEquipeNap(this IUsuarioAtual u) => Perfis.EquipeNap.Contains(u.Perfil);

    public static bool EhCoordenacao(this IUsuarioAtual u) =>
        u.Perfil is Perfis.Administrador or Perfis.CoordenadorNap;
}
