using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Agenda.Commands;
using Nap.Application.Features.Agenda.Queries;

namespace Nap.Api.Controllers;

/// <summary>Autorização para registrar atendimento fora da escala (ou em feriado). Não há <c>GET /{id}</c>: o Location aponta para a lista.</summary>
[ApiController]
[Route("api/escalas/autorizacoes")]
[Authorize(Policy = Politicas.PodeGerirProducao)]
public class AutorizacoesForaEscalaController : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AutorizacaoForaEscalaDto>> Listar(Guid? profissionalId, DateOnly? inicio, DateOnly? fim,
        [FromServices] ListarAutorizacoesForaEscalaHandler handler, CancellationToken ct, int pagina = 1, int tamanho = 20) =>
        handler.Handle(new ListarAutorizacoesForaEscalaQuery(profissionalId, inicio, fim, pagina, tamanho), ct);

    [HttpPost]
    [Idempotente(nameof(Listar))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Criar(CriarAutorizacaoForaEscalaRequest request, [FromServices] CriarAutorizacaoForaEscalaHandler handler,
        CancellationToken ct)
    {
        var id = await handler.Handle(new CriarAutorizacaoForaEscalaCommand(request.ProfissionalId, request.Inicio, request.Fim,
            request.Justificativa), ct);
        return CreatedAtAction(nameof(Listar), new { id }, new IdCriado(id));
    }

    [HttpPost("{id:guid}/revogar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revogar(Guid id, [FromServices] RevogarAutorizacaoForaEscalaHandler handler, CancellationToken ct)
    {
        await handler.Handle(new RevogarAutorizacaoForaEscalaCommand(id), ct);
        return NoContent();
    }
}
