using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nap.Application.Comum;

namespace Nap.Infrastructure.Persistencia;

/// <summary>Acumula a entrada no rastreador do contexto: ela é gravada no mesmo SaveChanges do próximo SalvarAsync.
/// Negados vão direto ao banco por um contexto de outro escopo.</summary>
public sealed class Auditoria(NapDbContext db, IUsuarioAtual usuario, IRelogio relogio, IServiceScopeFactory escopos,
    ILogger<Auditoria> logger) : IAuditoria
{
    public void Registrar(string acao, string recurso, Guid? recursoId) =>
        db.EventosAuditoria.Add(new EventoAuditoria(UsuarioOuSistema(), acao, recurso, recursoId, "sucesso", relogio.Agora));

    public async Task RegistrarNegadoAsync(string acao, string recurso, Guid? recursoId, CancellationToken ct, bool semUsuario = false)
    {
        try
        {
            var evento = new EventoAuditoria(semUsuario ? null : UsuarioOuSistema(), acao, recurso, recursoId, "negado", relogio.Agora);
            await using var escopo = escopos.CreateAsyncScope();
            var proprio = escopo.ServiceProvider.GetRequiredService<NapDbContext>();
            proprio.EventosAuditoria.Add(evento);
            await proprio.SaveChangesAsync(ct);
        }
        catch (Exception e)
        {
            // A negação já vai ser respondida; perder o registro não pode virar 500.
            logger.LogError(e, "Falha ao gravar auditoria negada {Acao}", acao);
        }
    }

    // Operações do sistema (sem usuário autenticado) ficam com usuário nulo.
    private Guid? UsuarioOuSistema()
    {
        try { return usuario.Id; }
        catch (UnauthorizedException) { return null; }
    }
}
