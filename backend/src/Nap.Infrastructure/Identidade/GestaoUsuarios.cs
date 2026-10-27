using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comum;
using Nap.Infrastructure.Persistencia;

namespace Nap.Infrastructure.Identidade;

/// <summary>
/// Administração de usuários sobre o <c>UserManager</c> (único uso fora do login). Cada operação é um único SaveChanges,
/// que leva junto a auditoria registrada antes pelo handler. Versão = <c>ConcurrencyStamp</c>. Mudança de perfil, curso ou
/// situação e redefinição de senha rotacionam o carimbo e revogam todas as sessões.
/// </summary>
public sealed class GestaoUsuarios(NapDbContext db, UserManager<UsuarioIdentidade> usuarios, ServicoSessao sessoes, IRelogio relogio)
    : IGestaoUsuarios
{
    public const string EmailJaCadastrado = "E-mail já cadastrado.";

    public Task<UsuarioAdminDto?> ObterAsync(Guid id, CancellationToken ct) =>
        Projetar(db.Users.AsNoTracking().Where(u => u.Id == id)).SingleOrDefaultAsync(ct);

    public async Task<PagedResult<UsuarioAdminDto>> ListarAsync(FiltroUsuarios f, CancellationToken ct)
    {
        var q = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(f.Busca))
        {
            var busca = f.Busca.Trim();
            q = q.Where(u => u.NomeCompleto.Contains(busca) || u.Email!.Contains(busca));
        }
        if (f.Perfil is { } perfil) q = q.Where(u => u.Perfil == perfil);
        if (f.CursoId is { } curso) q = q.Where(u => u.CursoId == curso);
        if (f.Ativo is { } ativo) q = q.Where(u => u.Ativo == ativo);

        var total = await q.CountAsync(ct);
        var itens = await Projetar(q.OrderBy(u => u.NomeCompleto).ThenBy(u => u.Id).Skip((f.Pagina - 1) * f.Tamanho).Take(f.Tamanho))
            .ToListAsync(ct);
        return new PagedResult<UsuarioAdminDto>(itens, f.Pagina, f.Tamanho, total);
    }

    public Task<int> ContarAdministradoresAtivosAsync(CancellationToken ct) =>
        db.Users.CountAsync(u => u.Ativo && u.Perfil == Perfis.Administrador, ct);

    public async Task<Guid> CriarAsync(NovoUsuario u, string senhaTemporaria, CancellationToken ct)
    {
        var usuario = new UsuarioIdentidade
        {
            Id = u.Id, UserName = u.Email, Email = u.Email, NomeCompleto = u.NomeCompleto, Perfil = u.Perfil, CursoId = u.CursoId,
            Ativo = true, AtivoDesde = relogio.Hoje, DeveTrocarSenha = true,
        };
        try
        {
            Exigir(await usuarios.CreateAsync(usuario, senhaTemporaria));
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Duas criações simultâneas passam pela validação do Identity; o índice único de NormalizedUserName barra uma.
            db.ChangeTracker.Clear();
            throw new ConflictException(EmailJaCadastrado);
        }
        return usuario.Id;
    }

    public async Task AtualizarAsync(Guid id, string nome, string perfil, Guid? cursoId, string versao, CancellationToken ct)
    {
        var usuario = await CarregarAsync(id, versao);
        var acessoMudou = usuario.Perfil != perfil || usuario.CursoId != cursoId;
        usuario.NomeCompleto = nome;
        usuario.Perfil = perfil;
        usuario.CursoId = cursoId;
        await SalvarAsync(usuario, revogar: acessoMudou);
    }

    public async Task AlterarSituacaoAsync(Guid id, bool ativo, string versao, CancellationToken ct)
    {
        var usuario = await CarregarAsync(id, versao);
        var mudou = usuario.Ativo != ativo;
        usuario.Ativo = ativo;
        // shortcut: um único período de inatividade [DesativadoEm, ReativadoEm); uma nova desativação descarta o anterior.
        // A produção e os ajustes leem esse período (ProfissionalProducaoDto.InativoEm), inclusive em semanas já fechadas.
        if (mudou && ativo) usuario.ReativadoEm = relogio.Hoje;
        else if (mudou) (usuario.DesativadoEm, usuario.ReativadoEm) = (relogio.Hoje, null);
        await SalvarAsync(usuario, revogar: mudou);
    }

    public async Task RedefinirSenhaAsync(Guid id, string senhaTemporaria, string versao, CancellationToken ct)
    {
        var usuario = await CarregarAsync(id, versao);
        // Hash direto (sem token de redefinição nem Remove+Add): um único SaveChanges, sem janela sem senha.
        foreach (var validador in usuarios.PasswordValidators)
            Exigir(await validador.ValidateAsync(usuarios, usuario, senhaTemporaria));
        usuario.PasswordHash = usuarios.PasswordHasher.HashPassword(usuario, senhaTemporaria);
        usuario.DeveTrocarSenha = true;
        usuario.AccessFailedCount = 0;
        usuario.LockoutEnd = null;
        await SalvarAsync(usuario, revogar: true);
    }

    private async Task<UsuarioIdentidade> CarregarAsync(Guid id, string versao)
    {
        var usuario = await usuarios.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("Usuário");
        // O UserStore (Attach + Update) descarta valores originais ajustados à mão, então a versão do If-Match é conferida
        // aqui contra a recém-lida; o UPDATE do Identity ainda confere a lida, cobrindo a janela até o SaveChanges.
        if (usuario.ConcurrencyStamp != versao)
        {
            db.ChangeTracker.Clear(); // a auditoria registrada pelo handler não pode ser gravada depois
            throw new ConflictException(UnidadeDeTrabalho.MensagemRegistroAlterado);
        }
        return usuario;
    }

    private async Task SalvarAsync(UsuarioIdentidade usuario, bool revogar)
    {
        // UpdateSecurityStampAsync já grava a entidade: carimbo novo e alterações no mesmo SaveChanges.
        Exigir(revogar ? await usuarios.UpdateSecurityStampAsync(usuario) : await usuarios.UpdateAsync(usuario));
        if (revogar)
            // Sem o token da requisição: o carimbo já foi gravado e a revogação não pode ser interrompida por um cancelamento.
            await sessoes.RevogarTodasAsync(usuario.Id, CancellationToken.None);
    }

    private void Exigir(IdentityResult resultado)
    {
        if (resultado.Succeeded) return;
        // A auditoria registrada pelo handler não pode ser gravada por um SaveChanges posterior.
        db.ChangeTracker.Clear();
        var codigos = resultado.Errors.Select(e => e.Code).ToList();
        if (codigos.Contains(nameof(IdentityErrorDescriber.ConcurrencyFailure)))
            throw new ConflictException(UnidadeDeTrabalho.MensagemRegistroAlterado);
        if (codigos.Any(c => c is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            throw new ConflictException(EmailJaCadastrado);
        throw new ValidationException(resultado.Errors
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? "senha" : "email")
            .ToDictionary(g => g.Key, g => g.Select(e => Traduzir(e, usuarios.Options.Password.RequiredLength)).ToArray()));
    }

    /// <summary>Mensagem em pt-BR para os erros do Identity que chegam ao usuário.</summary>
    internal static string Traduzir(IdentityError erro, int tamanhoMinimo) => erro.Code switch
    {
        nameof(IdentityErrorDescriber.PasswordTooShort) => $"A senha deve ter pelo menos {tamanhoMinimo} caracteres.",
        nameof(IdentityErrorDescriber.PasswordRequiresDigit) => "A senha deve ter pelo menos um número.",
        nameof(IdentityErrorDescriber.PasswordRequiresUpper) => "A senha deve ter pelo menos uma letra maiúscula.",
        nameof(IdentityErrorDescriber.PasswordRequiresLower) => "A senha deve ter pelo menos uma letra minúscula.",
        nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric) => "A senha deve ter pelo menos um símbolo.",
        nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars) => "A senha tem poucos caracteres diferentes.",
        nameof(IdentityErrorDescriber.PasswordMismatch) => "Senha incorreta.",
        nameof(IdentityErrorDescriber.InvalidEmail) or nameof(IdentityErrorDescriber.InvalidUserName) => "E-mail inválido.",
        _ => "Não foi possível salvar o usuário.",
    };

    private IQueryable<UsuarioAdminDto> Projetar(IQueryable<UsuarioIdentidade> q) =>
        q.Select(u => new UsuarioAdminDto(u.Id, u.NomeCompleto, u.Email ?? "", u.Perfil, u.CursoId,
            db.Cursos.Where(c => c.Id == u.CursoId).Select(c => c.Nome).FirstOrDefault(),
            u.Ativo, u.DeveTrocarSenha, u.ConcurrencyStamp ?? ""));
}
