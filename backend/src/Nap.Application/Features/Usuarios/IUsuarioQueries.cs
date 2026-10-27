using Nap.Application.Comum;
using Nap.Domain.Comum;

namespace Nap.Application.Features.Usuarios;

public record ProfissionalDto(Guid Id, string Nome, bool Ativo = true);

/// <summary>Profissional na produção; <c>DesativadoEm</c> nula com <c>Ativo = false</c> = desativado antes de a data ser registrada;
/// <c>AtivoDesde</c> nula = criado antes de a data ser registrada (sem limite inferior).</summary>
public record ProfissionalProducaoDto(Guid Id, string Nome, bool Ativo, DateOnly? DesativadoEm, string Perfil = Perfis.ProfissionalNap,
    DateOnly? AtivoDesde = null, DateOnly? ReativadoEm = null)
{
    /// <summary></summary>
    public bool InativoEm(DateOnly data) =>
        (!Ativo || ReativadoEm is not null) && data >= (DesativadoEm ?? DateOnly.MinValue) && data < (ReativadoEm ?? DateOnly.MaxValue);
}

public record UsuarioResumoDto(Guid Id, string Nome, string Perfil, Guid? CursoId, bool Ativo);

public interface IUsuarioQueries
{
    Task<bool> EhProfissionalAtivoAsync(Guid id, CancellationToken ct);
    /// <summary>Usuários de perfil profissional ativos; com <paramref name="incluirInativos"/>, também os inativos.</summary>
    Task<IReadOnlyList<ProfissionalDto>> ListarProfissionaisAsync(bool incluirInativos, CancellationToken ct);

    /// <summary>
    /// Usuários de perfil profissional para a produção de [<paramref name="inicio"/>, <paramref name="fim"/>]: os ativos e os inativos
    /// com algum fato na janela (atendimento, atividade, realização, ajuste, fechamento). Com <paramref name="profissionalId"/>, só ele,
    /// ativo ou não.
    /// </summary>
    Task<IReadOnlyList<ProfissionalProducaoDto>> ListarProfissionaisDaProducaoAsync(DateOnly inicio, DateOnly fim, TimeZoneInfo fuso,
        Guid? profissionalId, CancellationToken ct);
    /// <summary>Ids dos usuários ativos do perfil; com <paramref name="cursoId"/>, só os vinculados a esse curso.</summary>
    Task<IReadOnlyList<Guid>> ListarAtivosPorPerfilAsync(string perfil, Guid? cursoId, CancellationToken ct);
    Task<bool> EstaAtivoAsync(Guid id, CancellationToken ct);
    Task<UsuarioResumoDto?> ObterResumoAsync(Guid id, CancellationToken ct);
    /// <summary>Usuários ativos de todos os perfis, por nome.</summary>
    Task<IReadOnlyList<UsuarioResumoDto>> ListarAtivosAsync(CancellationToken ct);
}

public static class ExtensoesUsuarioQueries
{
    /// <summary>Profissional alvo de um registro: o informado ou o próprio usuário. Outro profissional só pela coordenação e
    /// se for membro ativo da equipe NAP.</summary>
    public static async Task<Guid> ResolverProfissionalAsync(this IUsuarioQueries usuarios, IUsuarioAtual usuario, Guid? profissionalId,
        CancellationToken ct)
    {
        var id = profissionalId ?? usuario.Id;
        if (id != usuario.Id)
        {
            if (!usuario.EhCoordenacao()) throw new ForbiddenException();
            if (!await usuarios.EhProfissionalAtivoAsync(id, ct))
                throw Validacao.ErroDeCampo("profissionalId", "O profissional deve ser um membro ativo da equipe NAP.");
        }
        return id;
    }
}
