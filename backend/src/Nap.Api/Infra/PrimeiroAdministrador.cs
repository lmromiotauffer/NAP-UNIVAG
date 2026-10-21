using Nap.Application.Features.Usuarios;
using Nap.Application.Features.Usuarios.Commands;
using Nap.Domain.Comum;

namespace Nap.Api.Infra;

/// <summary>
/// <c>--criar-admin &lt;email&gt; "&lt;nome&gt;"</c>: único jeito de entrar num banco de produção novo (o semeador recusa produção).
/// Só funciona sem administrador ativo; a senha é temporária e o primeiro acesso exige troca. Sem auditoria: não há usuário atual.
/// </summary>
public static class PrimeiroAdministrador
{
    public static async Task<string> CriarAsync(IServiceProvider servicos, string email, string nome, CancellationToken ct = default)
    {
        var gestao = servicos.GetRequiredService<IGestaoUsuarios>();
        if (await gestao.ContarAdministradoresAtivosAsync(ct) > 0)
            throw new InvalidOperationException("Já existe administrador ativo; crie os demais usuários pela tela Administração.");

        var comando = new CriarUsuarioCommand(email, nome, Perfis.Administrador, null);
        var resultado = await new CriarUsuarioValidator().ValidateAsync(comando, ct);
        if (!resultado.IsValid)
            throw new InvalidOperationException(string.Join(" ", resultado.Errors.Select(e => e.ErrorMessage)));

        var senha = GeradorSenhaTemporaria.Gerar();
        await gestao.CriarAsync(new NovoUsuario(Guid.CreateVersion7(), email.Trim(), nome.Trim(), Perfis.Administrador, null), senha, ct);
        return senha;
    }
}
