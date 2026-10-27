using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Domain.Comum;
using Nap.Domain.Peis;

namespace Nap.Application.Features.Peis;

public static class AutorizacaoPei
{
    /// <summary>Só a Equipe NAP e o coordenador de curso usam o PEI; os demais perfis recebem 403 antes de qualquer busca.</summary>
    public static void ExigirAcesso(IUsuarioAtual u)
    {
        if (!u.EhEquipeNap() && u.Perfil != Perfis.CoordenadorCurso) throw new ForbiddenException();
    }

    /// <summary>Coordenador de curso só vê versões publicadas (inclusive as depois arquivadas) no curso dele e, depois de uma
    /// transferência, a versão publicada em vigor do aluno que hoje está no curso dele.</summary>
    public static bool PodeVer(IUsuarioAtual u, VersaoPei v, Guid? cursoAtualAluno = null) =>
        u.EhEquipeNap() || (u.Perfil == Perfis.CoordenadorCurso && v.PublicadaEm is not null && u.CursoId is not null
            && (u.CursoId == v.CursoPublicacaoId || (v.Estado == EstadoVersaoPei.Publicada && u.CursoId == cursoAtualAluno)));

    /// <summary>Criar, editar, enviar para revisão, gerar acesso do aluno, nova versão e descarte.</summary>
    public static bool PodeElaborar(IUsuarioAtual u) => u.EhEquipeNap();

    /// <summary>Aprovar, devolver, publicar e arquivar.</summary>
    public static bool PodeRevisarEPublicar(IUsuarioAtual u) => u.EhCoordenacao();

    public static bool PodeAssinarComoProfissional(IUsuarioAtual u, Pei p) => PodeAssinarComoProfissional(u, p.ProfissionalResponsavelId);

    public static bool PodeAssinarComoProfissional(IUsuarioAtual u, Guid responsavelId) => u.Id == responsavelId;

    public static bool PodeRegistrarCiencia(IUsuarioAtual u, VersaoPei v, Guid? cursoAtualAluno) =>
        PodeRegistrarCiencia(u, v.CursoPublicacaoId, cursoAtualAluno);

    /// <summary>Coordenador do curso da publicação ou do curso atual do aluno (quem aplica o plano depois da transferência).</summary>
    public static bool PodeRegistrarCiencia(IUsuarioAtual u, Guid? cursoPublicacaoId, Guid? cursoAtualAluno = null) =>
        u.Perfil == Perfis.CoordenadorCurso && u.CursoId is not null && (u.CursoId == cursoPublicacaoId || u.CursoId == cursoAtualAluno);

    /// <summary>Dados mínimos da versão para decidir as ações, sem carregar o agregado (consulta de leitura).</summary>
    public record SituacaoVersao(Guid ResponsavelId, Guid? CursoPublicacaoId, EstadoVersaoPei Estado, bool TemConteudoPedagogico,
        bool ProfissionalAssinou, bool AlunoAssinou, bool MaisRecente, bool PeiImpedeNovaVersao, bool CienciaRegistradaPorMim,
        Guid? CursoAtualAlunoId = null);

    /// <summary>Mesmas permissões dos comandos (perfil) somadas às condições de estado que o domínio exige.</summary>
    public static AcoesVersaoDto Acoes(IUsuarioAtual u, SituacaoVersao s)
    {
        var elabora = PodeElaborar(u);
        var revisa = PodeRevisarEPublicar(u);
        var e = s.Estado;
        return new AcoesVersaoDto(
            PodeEditar: elabora && VersaoPei.PodeEditar(e),
            PodeEnviarRevisao: elabora && VersaoPei.PodeEnviarRevisao(e) && s.TemConteudoPedagogico,
            PodeAprovar: revisa && VersaoPei.PodeAprovar(e),
            PodeDevolver: revisa && VersaoPei.PodeDevolver(e),
            PodeAssinarComoProfissional: PodeAssinarComoProfissional(u, s.ResponsavelId) && VersaoPei.PodeRegistrarAssinatura(e)
                && !s.ProfissionalAssinou,
            PodeGerarAcessoAluno: elabora && VersaoPei.PodeRegistrarAssinatura(e) && !s.AlunoAssinou,
            PodePublicar: revisa && Pei.PodePublicar(e, s.MaisRecente),
            PodeArquivar: revisa && VersaoPei.PodeArquivar(e),
            PodeRegistrarCiencia: PodeRegistrarCiencia(u, s.CursoPublicacaoId, s.CursoAtualAlunoId) && VersaoPei.PodeRegistrarCiencia(e)
                && !s.CienciaRegistradaPorMim,
            PodeCriarNovaVersao: elabora && Pei.PodeCriarNovaVersao(s.PeiImpedeNovaVersao));
    }

    /// <summary>A versão com o seu PEI; 404 se não existe ou está fora do escopo do usuário.</summary>
    public static async Task<(Pei Pei, VersaoPei Versao)> ObterVersaoAsync(IPeiRepository peis, IUsuarioAtual u,
        Guid versaoId, CancellationToken ct)
    {
        var (pei, versao, _) = await ObterVersaoAsync(peis, null, u, versaoId, ct);
        return (pei, versao);
    }

    /// <summary>Com os alunos: também devolve o curso atual do aluno e admite o coordenador desse curso (<see cref="PodeVer"/>).</summary>
    public static async Task<(Pei Pei, VersaoPei Versao, Guid? CursoAtualAluno)> ObterVersaoAsync(IPeiRepository peis,
        IAlunoRepository? alunos, IUsuarioAtual u, Guid versaoId, CancellationToken ct)
    {
        var pei = await peis.ObterPorVersaoAsync(versaoId, ct);
        var versao = pei?.Versoes.SingleOrDefault(v => v.Id == versaoId);
        var cursoAtual = pei is not null && alunos is not null && u.Perfil == Perfis.CoordenadorCurso
            ? (await alunos.ObterAsync(pei.AlunoId, ct))?.CursoAtualId : null;
        if (pei is null || versao is null || !PodeVer(u, versao, cursoAtual)) throw new NotFoundException("Versão do PEI");
        return (pei, versao, cursoAtual);
    }
}
