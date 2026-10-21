namespace Nap.Application.Comum;

/// <summary>Registro gravado junto do próximo SalvarAsync. Nunca recebe conteúdo clínico.</summary>
public interface IAuditoria
{
    void Registrar(string acao, string recurso, Guid? recursoId);

    /// <summary>
    /// Tentativa negada (<c>Resultado = "negado"</c>), gravada na hora em contexto próprio: sobrevive ao rollback da requisição.
    /// Falha ao gravar é só logada. Com <paramref name="semUsuario"/>, o evento fica sem usuário mesmo com sessão na requisição (login).
    /// </summary>
    Task RegistrarNegadoAsync(string acao, string recurso, Guid? recursoId, CancellationToken ct, bool semUsuario = false);
}
