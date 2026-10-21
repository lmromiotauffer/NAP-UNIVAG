using Nap.Application.Comum;

namespace Nap.Application.Tests.Fakes;

public sealed class UsuarioAtualFalso(string perfil, Guid? cursoId = null) : IUsuarioAtual
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Perfil { get; } = perfil;
    public Guid? CursoId { get; } = cursoId;
}
