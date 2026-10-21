using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Agenda.Commands;
using Nap.Application.Features.Agenda.Queries;

namespace Nap.Api.Controllers;

[ApiController]
[Route("api/escalas")]
[Authorize(Policy = Politicas.PodeAdministrar)]
public class EscalasController : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<EscalaProfissionalDto>> Listar(Guid? profissionalId, [FromServices] ListarEscalasHandler handler,
        CancellationToken ct) =>
        handler.Handle(new ListarEscalasQuery(profissionalId), ct);

    /// <summary>Não há <c>GET /escalas/{id}</c>: o Location aponta para a lista.</summary>
    [HttpPost]
    [Idempotente(nameof(Listar))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Criar(CriarEscalaRequest request, [FromServices] CriarEscalaHandler handler, CancellationToken ct)
    {
        var id = await handler.Handle(new CriarEscalaCommand(request.ProfissionalId, request.VigenciaInicio, request.VigenciaFim,
            request.Faixas), ct);
        return CreatedAtAction(nameof(Listar), new { id }, new IdCriado(id));
    }

    /// <summary>Encerrar ou reabrir a vigência (fim nulo = aberta).</summary>
    [HttpPut("{id:guid}/vigencia")]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarVigencia(Guid id, AtualizarVigenciaEscalaRequest request,
        [FromServices] AtualizarVigenciaEscalaHandler handler, CancellationToken ct)
    {
        await handler.Handle(new AtualizarVigenciaEscalaCommand(id, request.VigenciaFim, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }
}
