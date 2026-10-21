using Nap.Application.Features.Usuarios;

namespace Nap.Application.Tests.Fakes;

public sealed class UsuarioQueriesFalso(params Guid[] profissionaisAtivos) : IUsuarioQueries
{
    public List<(Guid Id, string Perfil, Guid? CursoId, bool Ativo)> Usuarios { get; } = [];
    /// <summary>Nome por id; sem entrada, "Usuário".</summary>
    public Dictionary<Guid, string> Nomes { get; } = [];
    /// <summary>Profissionais inativos devolvidos pela consulta da produção.</summary>
    public List<ProfissionalProducaoDto> Inativos { get; } = [];
    /// <summary>Janela recebida na última consulta da produção.</summary>
    public (DateOnly Inicio, DateOnly Fim)? JanelaRecebida { get; private set; }

    public Task<bool> EhProfissionalAtivoAsync(Guid id, CancellationToken ct) => Task.FromResult(profissionaisAtivos.Contains(id));

    public Task<IReadOnlyList<ProfissionalDto>> ListarProfissionaisAsync(bool incluirInativos, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProfissionalDto>>(profissionaisAtivos.Select(id => new ProfissionalDto(id, Nomes.GetValueOrDefault(id, "Usuário"))).ToList());

    public Task<IReadOnlyList<ProfissionalProducaoDto>> ListarProfissionaisDaProducaoAsync(DateOnly inicio, DateOnly fim, TimeZoneInfo fuso,
        Guid? profissionalId, CancellationToken ct)
    {
        JanelaRecebida = (inicio, fim);
        return Task.FromResult<IReadOnlyList<ProfissionalProducaoDto>>(profissionaisAtivos.Where(id => profissionalId == null || id == profissionalId)
            .Select(id => new ProfissionalProducaoDto(id, Nomes.GetValueOrDefault(id, "Usuário"), true, null))
            .Concat(Inativos.Where(p => profissionalId == null || p.Id == profissionalId)).ToList());
    }

    public Task<IReadOnlyList<Guid>> ListarAtivosPorPerfilAsync(string perfil, Guid? cursoId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(Usuarios
            .Where(u => u.Ativo && u.Perfil == perfil && (cursoId == null || u.CursoId == cursoId)).Select(u => u.Id).ToList());

    public Task<bool> EstaAtivoAsync(Guid id, CancellationToken ct) => Task.FromResult(Usuarios.Any(u => u.Id == id && u.Ativo));

    public Task<UsuarioResumoDto?> ObterResumoAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Usuarios.Where(u => u.Id == id).Select(Resumo).FirstOrDefault());

    public Task<IReadOnlyList<UsuarioResumoDto>> ListarAtivosAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<UsuarioResumoDto>>(Usuarios.Where(u => u.Ativo).Select(Resumo)
            .OrderBy(u => u.Nome, StringComparer.Ordinal).ThenBy(u => u.Id).ToList());

    private UsuarioResumoDto Resumo((Guid Id, string Perfil, Guid? CursoId, bool Ativo) u) =>
        new(u.Id, Nomes.GetValueOrDefault(u.Id, "Usuário"), u.Perfil, u.CursoId, u.Ativo);
}
