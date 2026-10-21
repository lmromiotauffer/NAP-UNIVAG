using Nap.Application.Comum;
using Nap.Domain.Comum;

namespace Nap.Application.Features.Usuarios;

/// <summary>Alçada da administração de usuários: só Administrador gerencia administradores; ninguém altera o próprio acesso.</summary>
public static class RegrasAlcada
{
    /// <summary>Primeira checagem dos handlers, antes da validação: quem não administra não recebe erros de campo.</summary>
    public static void ExigirAdministracao(IUsuarioAtual u)
    {
        if (!u.EhCoordenacao()) throw new ForbiddenException();
    }

    public static void ExigirGerenciar(IUsuarioAtual u, string perfilAlvo)
    {
        if (u.Perfil != Perfis.Administrador && (u.Perfil != Perfis.CoordenadorNap || perfilAlvo == Perfis.Administrador))
            throw new ForbiddenException();
    }

    public static void ExigirNaoProprio(IUsuarioAtual u, Guid alvoId)
    {
        if (alvoId == u.Id) throw new ForbiddenException("Você não pode alterar o próprio acesso.");
    }

    /// <summary>O último administrador ativo não pode ser inativado nem trocar de perfil (→ 422).</summary>
    public static async Task ExigirOutroAdministradorAsync(IGestaoUsuarios gestao, UsuarioAdminDto alvo, CancellationToken ct)
    {
        if (alvo is not { Perfil: Perfis.Administrador, Ativo: true }) return;
        // ponytail: trava de aplicação se virar problema — duas inativações simultâneas podem passar ambas por esta contagem.
        if (await gestao.ContarAdministradoresAtivosAsync(ct) <= 1)
            throw new DomainException("O último administrador ativo não pode ser inativado nem ter o perfil trocado.");
    }

    /// <summary>Coordenador de curso exige curso; os demais perfis não têm curso (regra de entrada).</summary>
    public static bool CursoCoerente(string perfil, Guid? cursoId) =>
        perfil == Perfis.CoordenadorCurso ? cursoId is not null && cursoId != Guid.Empty : cursoId is null;

    public const string MensagemCurso = "Coordenador de curso exige um curso; os demais perfis não têm curso.";
}
