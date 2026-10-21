using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Features.Agenda.Commands;
using Nap.Application.Features.Agenda.Queries;

namespace Nap.Api.Controllers;

/// <summary>Consulta: qualquer autenticado (a agenda mostra). Escrita: <c>PodeAdministrar</c>.</summary>
[ApiController]
[Route("api/feriados")]
[Authorize]
public class FeriadosController : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<FeriadoDto>> Listar(int ano, [FromServices] ListarFeriadosHandler handler, CancellationToken ct) =>
        handler.Handle(new ListarFeriadosQuery(ano), ct);

    /// <summary>Devolve os compromissos e atendimentos da data (<c>conflitos</c>); nada é alterado. Sem <c>Idempotente</c>: o corpo traz os conflitos.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.PodeAdministrar)]
    [ProducesResponseType<FeriadoCriadoDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Criar(CriarFeriadoRequest request, [FromServices] CriarFeriadoHandler handler, CancellationToken ct)
    {
        var criado = await handler.Handle(new CriarFeriadoCommand(request.Data, request.Descricao), ct);
        return CreatedAtAction(nameof(Listar), new { ano = request.Data.Year }, criado);
    }

    [HttpPost("{id:guid}/inativar")]
    [Authorize(Policy = Politicas.PodeAdministrar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Inativar(Guid id, [FromServices] InativarFeriadoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new InativarFeriadoCommand(id), ct);
        return NoContent();
    }
}
