using Nap.Application.Comum;
using Nap.Domain.Comum;

namespace Nap.Application.Features.Peis;

/// <summary>Recorte por perfil aplicado na consulta; espelha <see cref="AutorizacaoPei.PodeVer"/>.</summary>
public record EscopoPeis(bool Todos, Guid? SomenteCursoId)
{
    /// <summary>Equipe NAP vê tudo; coordenador de curso só versões publicadas no curso dele, sem a demanda interna.</summary>
    public static EscopoPeis Para(IUsuarioAtual u) =>
        u.EhEquipeNap() ? new(true, null)
        : u.Perfil == Perfis.CoordenadorCurso ? new(false, u.CursoId ?? throw new ForbiddenException())
        : throw new ForbiddenException();
}
