using Nap.Application.Comum;

namespace Nap.Application.Features.Auditoria;

/// <summary><c>Acao</c> filtra por prefixo; <c>Inicio</c>/<c>Fim</c> são datas civis no fuso institucional (fim inclusivo).</summary>
public record FiltroAuditoria(DateOnly Inicio, DateOnly Fim, Guid? UsuarioId, string? Acao, string? Recurso, Guid? RecursoId,
    string? Resultado, int Pagina, int Tamanho);

public interface IAuditoriaQueries
{
    /// <summary>Mais recentes primeiro.</summary>
    Task<PagedResult<EventoAuditoriaDto>> ListarAsync(FiltroAuditoria filtro, CancellationToken ct);
}
