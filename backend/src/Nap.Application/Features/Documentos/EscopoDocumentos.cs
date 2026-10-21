using Nap.Application.Comum;
using Nap.Domain.Comum;
using Nap.Domain.Documentos;

namespace Nap.Application.Features.Documentos;

/// <summary>Recorte por perfil aplicado na consulta antes de contar e paginar; <see cref="PodeVer"/> é a mesma regra em memória.</summary>
public record EscopoDocumentos(Guid? SomenteCursoId, bool IncluirArquivados)
{
    /// <summary>Equipe NAP vê os documentos do aluno; arquivados só para Administrador/CoordenadorNap que os pedirem.
    /// Coordenador de curso só vê compartilhados, não arquivados, gravados com o curso dele; demais perfis são negados.</summary>
    public static EscopoDocumentos Para(IUsuarioAtual u, bool incluirArquivados) =>
        u.EhEquipeNap() ? new(null, incluirArquivados && u.EhCoordenacao())
        : u.Perfil == Perfis.CoordenadorCurso ? new(u.CursoId ?? throw new ForbiddenException(), false)
        : throw new ForbiddenException();

    public bool PodeVer(Documento d) =>
        (IncluirArquivados || !d.Arquivado)
        && (SomenteCursoId is not { } curso
            || (d.CursoId == curso && d.Visibilidade == VisibilidadeDocumento.CompartilhadoCoordenacao));
}
