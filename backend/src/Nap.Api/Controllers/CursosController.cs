using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Features.Cursos;
using Nap.Application.Features.Cursos.Commands;

namespace Nap.Api.Controllers;

/// <summary>Lista para filtros e formulários: qualquer usuário autenticado. Escrita: <c>PodeAdministrar</c>.</summary>
[ApiController]
[Route("api/cursos")]
[Authorize]
public class CursosController : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CursoDto>> Listar([FromServices] ICursoQueries cursos, CancellationToken ct) =>
        cursos.ListarAsync(ct);

    /// <summary>Não há <c>GET /cursos/{id}</c>: o Location aponta para a lista.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.PodeAdministrar)]
    [Idempotente(nameof(Listar))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Criar(CriarCursoRequest request, [FromServices] CriarCursoHandler handler, CancellationToken ct)
    {
        var id = await handler.Handle(new CriarCursoCommand(request.Nome), ct);
        return CreatedAtAction(nameof(Listar), new { id }, new IdCriado(id));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Politicas.PodeAdministrar)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Renomear(Guid id, RenomearCursoRequest request, [FromServices] RenomearCursoHandler handler,
        CancellationToken ct)
    {
        await handler.Handle(new RenomearCursoCommand(id, request.Nome, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/situacao")]
    [Authorize(Policy = Politicas.PodeAdministrar)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AlterarSituacao(Guid id, AlterarSituacaoRequest request,
        [FromServices] AlterarSituacaoCursoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new AlterarSituacaoCursoCommand(id, request.Ativo, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }
}
