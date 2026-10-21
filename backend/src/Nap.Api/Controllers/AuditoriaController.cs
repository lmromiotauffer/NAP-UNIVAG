using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Infra;
using Nap.Application.Comum;
using Nap.Application.Features.Auditoria;
using Nap.Application.Features.Auditoria.Queries;

namespace Nap.Api.Controllers;

/// <summary>Trilha de auditoria: somente leitura (não há rota de escrita).</summary>
[ApiController]
[Route("api/auditoria")]
[Authorize(Policy = Politicas.PodeAdministrar)]
public class AuditoriaController : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<EventoAuditoriaDto>> Listar(DateOnly? inicio, DateOnly? fim, Guid? usuarioId, string? acao, string? recurso,
        Guid? recursoId, string? resultado, [FromServices] ListarAuditoriaHandler handler, CancellationToken ct, int pagina = 1,
        int tamanho = 20) =>
        handler.Handle(new ListarAuditoriaQuery(inicio, fim, usuarioId, acao, recurso, recursoId, resultado, pagina, tamanho), ct);
}
