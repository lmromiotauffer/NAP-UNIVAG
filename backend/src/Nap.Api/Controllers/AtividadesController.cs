using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Comum;
using Nap.Application.Features.Atividades;
using Nap.Application.Features.Atividades.Commands;
using Nap.Application.Features.Atividades.Queries;
using Nap.Domain.Atividades;

namespace Nap.Api.Controllers;

[ApiController]
[Route("api/atividades")]
[Authorize]
public class AtividadesController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Politicas.PodeConsultarAtividades)]
    public Task<PagedResult<AtividadeDto>> Listar([FromServices] ListarAtividadesHandler handler, DateOnly? inicio, DateOnly? fim,
        Guid? cursoId, Guid? profissionalId, TipoAtividade? tipo, bool? institucionais, CancellationToken ct, int pagina = 1,
        int tamanho = 20) =>
        handler.Handle(new ListarAtividadesQuery(inicio, fim, cursoId, profissionalId, tipo, institucionais, pagina, tamanho), ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Politicas.PodeConsultarAtividades)]
    public Task<AtividadeDetalheDto> Obter(Guid id, [FromServices] ObterAtividadeHandler handler, CancellationToken ct) =>
        handler.Handle(new ObterAtividadeQuery(id), ct);

    [HttpPost]
    [Authorize(Policy = Politicas.PodeRegistrarAtividades)]
    [Idempotente(nameof(Obter))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Registrar(RegistrarAtividadeRequest request, [FromServices] RegistrarAtividadeHandler handler,
        CancellationToken ct)
    {
        var id = await handler.Handle(new RegistrarAtividadeCommand(request.CompromissoId, request.ProfissionalId, request.Inicio,
            request.Fim, request.CursoId, request.Tipo, request.Descricao, request.JustificativaAvulso), ct);
        return CreatedAtAction(nameof(Obter), new { id }, new IdCriado(id));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Politicas.PodeRegistrarAtividades)]
    [ExigeIfMatch]
    public Task<AtividadeDetalheDto> Corrigir(Guid id, CorrigirAtividadeRequest request, [FromServices] CorrigirAtividadeHandler handler,
        CancellationToken ct) =>
        handler.Handle(new CorrigirAtividadeCommand(id, HttpContext.VersaoIfMatch(), request.Inicio, request.Fim, request.CursoId,
            request.Tipo, request.Descricao, request.Justificativa), ct);
}
