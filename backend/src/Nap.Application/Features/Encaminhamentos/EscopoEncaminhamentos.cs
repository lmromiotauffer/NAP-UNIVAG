using Nap.Application.Comum;
using Nap.Domain.Comum;

namespace Nap.Application.Features.Encaminhamentos;

/// <summary>Recorte por perfil aplicado na consulta antes de contar e paginar; espelha <see cref="AutorizacaoEncaminhamento.PodeVer"/>.</summary>
public record EscopoEncaminhamentos(bool Todos, bool SoDestinosSip, Guid? SomenteCursoId, bool VerMotivo, bool VerObservacoes)
{
    /// <summary>Equipe NAP vê tudo; SIP só destinos SIP/Clínica Escola, sem observações; coordenador de curso só o curso
    /// gravado na criação, sem motivo nem observações; demais perfis são negados.</summary>
    public static EscopoEncaminhamentos Para(IUsuarioAtual u) => u.Perfil switch
    {
        Perfis.Administrador or Perfis.CoordenadorNap or Perfis.ProfissionalNap => new(true, false, null, true, true),
        Perfis.Sip => new(false, true, null, true, false),
        Perfis.CoordenadorCurso => new(false, false, u.CursoId ?? throw new ForbiddenException(), false, false),
        _ => throw new ForbiddenException(),
    };
}
