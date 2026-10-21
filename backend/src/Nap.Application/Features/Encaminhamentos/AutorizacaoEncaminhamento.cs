using Nap.Application.Comum;
using Nap.Domain.Comum;
using Nap.Domain.Encaminhamentos;

namespace Nap.Application.Features.Encaminhamentos;

public static class AutorizacaoEncaminhamento
{
    public static bool PodeVer(IUsuarioAtual u, Encaminhamento e) => u.Perfil switch
    {
        Perfis.Administrador or Perfis.CoordenadorNap or Perfis.ProfissionalNap => true,
        Perfis.Sip => e.Destino is DestinoEncaminhamento.Sip or DestinoEncaminhamento.ClinicaEscola,
        Perfis.CoordenadorCurso => u.CursoId is not null && u.CursoId == e.CursoId,
        _ => false,
    };

    public static bool PodeRegistrarRetorno(IUsuarioAtual u, Encaminhamento e) => PodeRegistrarRetorno(u, e.Destino, e.CursoId);

    /// <summary>Mesma regra, a partir dos campos projetados pelas consultas.</summary>
    public static bool PodeRegistrarRetorno(IUsuarioAtual u, DestinoEncaminhamento destino, Guid cursoId) => destino switch
    {
        DestinoEncaminhamento.Sip or DestinoEncaminhamento.ClinicaEscola =>
            u.Perfil is Perfis.Sip || u.EhCoordenacao(),
        DestinoEncaminhamento.Externo => u.EhEquipeNap(),
        DestinoEncaminhamento.CoordenacaoCurso =>
            u.EhCoordenacao() || (u.Perfil is Perfis.CoordenadorCurso && u.CursoId == cursoId),
        _ => false,
    };
}
