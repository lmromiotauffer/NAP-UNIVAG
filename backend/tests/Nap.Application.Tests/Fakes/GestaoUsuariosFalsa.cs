using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Tests.Fakes;

/// <summary>Porta de identidade em memória; registra as chamadas e se a auditoria veio antes delas.</summary>
public sealed class GestaoUsuariosFalsa(AuditoriaFalsa auditoria) : IGestaoUsuarios
{
    public Dictionary<Guid, UsuarioAdminDto> Usuarios { get; } = [];
    public List<string> Chamadas { get; } = [];
    public bool AuditoriaAntesDaPorta { get; private set; }
    public string? UltimaSenha { get; private set; }
    public string? UltimaVersao { get; private set; }

    public UsuarioAdminDto Adicionar(string perfil, Guid? cursoId = null, bool ativo = true, Guid? id = null)
    {
        var u = new UsuarioAdminDto(id ?? Guid.NewGuid(), "Usuária de teste", $"{Guid.NewGuid():N}@teste.nap", perfil, cursoId,
            null, ativo, false, "v1");
        Usuarios[u.Id] = u;
        return u;
    }

    private void Registrar(string chamada)
    {
        Chamadas.Add(chamada);
        AuditoriaAntesDaPorta = auditoria.Registros.Count > 0;
    }

    public Task<UsuarioAdminDto?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Usuarios.GetValueOrDefault(id));

    public Task<PagedResult<UsuarioAdminDto>> ListarAsync(FiltroUsuarios f, CancellationToken ct) =>
        Task.FromResult(new PagedResult<UsuarioAdminDto>([.. Usuarios.Values], f.Pagina, f.Tamanho, Usuarios.Count));

    public Task<int> ContarAdministradoresAtivosAsync(CancellationToken ct) =>
        Task.FromResult(Usuarios.Values.Count(u => u.Ativo && u.Perfil == Nap.Domain.Comum.Perfis.Administrador));

    public Task<Guid> CriarAsync(NovoUsuario u, string senhaTemporaria, CancellationToken ct)
    {
        Registrar("criar");
        UltimaSenha = senhaTemporaria;
        Usuarios[u.Id] = new UsuarioAdminDto(u.Id, u.NomeCompleto, u.Email, u.Perfil, u.CursoId, null, true, true, "v1");
        return Task.FromResult(u.Id);
    }

    public Task AtualizarAsync(Guid id, string nome, string perfil, Guid? cursoId, string versao, CancellationToken ct)
    {
        Registrar("atualizar");
        Usuarios[id] = Usuarios[id] with { Nome = nome, Perfil = perfil, CursoId = cursoId };
        return Task.CompletedTask;
    }

    public Task AlterarSituacaoAsync(Guid id, bool ativo, string versao, CancellationToken ct)
    {
        Registrar("situacao");
        Usuarios[id] = Usuarios[id] with { Ativo = ativo };
        return Task.CompletedTask;
    }

    public Task RedefinirSenhaAsync(Guid id, string senhaTemporaria, string versao, CancellationToken ct)
    {
        Registrar("senha");
        UltimaSenha = senhaTemporaria;
        UltimaVersao = versao;
        return Task.CompletedTask;
    }
}
