using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Comum;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Producao.Commands;
using Nap.Application.Features.Producao.Queries;

namespace Nap.Api.Controllers;

[ApiController]
[Route("api/producao")]
[Authorize]
public class ProducaoController : ControllerBase
{
    /// <param name="referencia">Qualquer data do período (yyyy-MM-dd).</param>
    [HttpGet]
    [Authorize(Policy = Politicas.PodeConsultarProducao)]
    public Task<ProducaoDto> Obter(TipoPeriodo tipo, DateOnly referencia, Guid? profissionalId, Guid? cursoId,
        [FromServices] ObterProducaoHandler handler, CancellationToken ct) =>
        handler.Handle(new ObterProducaoQuery(tipo, referencia, profissionalId, cursoId), ct);

    /// <summary>Mesmo recorte de <c>GET /api/producao</c>; anexo sem sniffing e sem cache.</summary>
    /// <param name="formato">pdf ou xlsx.</param>
    [HttpGet("exportar")]
    [Authorize(Policy = Politicas.PodeConsultarProducao)]
    [SemRastro]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, "application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> Exportar(string? formato, TipoPeriodo tipo, DateOnly referencia, Guid? profissionalId, Guid? cursoId,
        [FromServices] ExportarProducaoHandler handler, CancellationToken ct)
    {
        var arquivo = await handler.Handle(new ExportarProducaoQuery(formato ?? "", tipo, referencia, profissionalId, cursoId), ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(arquivo.Conteudo, arquivo.ContentType, arquivo.NomeArquivo, enableRangeProcessing: false);
    }

    [HttpGet("semanas")]
    [Authorize(Policy = Politicas.EquipeNap)]
    public Task<IReadOnlyList<SemanaProducaoDto>> Semanas(Guid? profissionalId, DateOnly inicio, DateOnly fim,
        [FromServices] ListarSemanasHandler handler, CancellationToken ct) =>
        handler.Handle(new ListarSemanasQuery(profissionalId, inicio, fim), ct);

    [HttpPost("semanas/fechar")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Fechar(FecharSemanaRequest request, [FromServices] FecharSemanaHandler handler, CancellationToken ct)
    {
        await handler.Handle(new FecharSemanaCommand(request.ProfissionalId, request.Semana, request.ConfirmarPendencias), ct);
        return NoContent();
    }

    [HttpPost("semanas/reabrir")]
    [Authorize(Policy = Politicas.PodeGerirProducao)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reabrir(ReabrirSemanaRequest request, [FromServices] ReabrirSemanaHandler handler, CancellationToken ct)
    {
        await handler.Handle(new ReabrirSemanaCommand(request.ProfissionalId, request.Semana, request.Justificativa, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpGet("ajustes")]
    [Authorize(Policy = Politicas.PodeGerirProducao)]
    public Task<IReadOnlyList<AjusteProducaoDto>> Ajustes(Guid? profissionalId, DateOnly inicio, DateOnly fim,
        [FromServices] ListarAjustesHandler handler, CancellationToken ct) =>
        handler.Handle(new ListarAjustesQuery(profissionalId, inicio, fim), ct);

    [HttpPost("ajustes")]
    [Authorize(Policy = Politicas.PodeGerirProducao)]
    [Idempotente(nameof(Ajustes))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CriarAjuste(CriarAjusteRequest request, [FromServices] CriarAjusteHandler handler,
        [FromServices] IRelogio relogio, CancellationToken ct)
    {
        var id = await handler.Handle(new CriarAjusteCommand(request.ProfissionalId, request.Data, request.Categoria, request.Ocorrencias,
            request.Minutos, request.Justificativa), ct);
        return CreatedAtAction(nameof(Ajustes), request.ValoresRota(relogio.Fuso), new IdCriado(id));
    }
}
