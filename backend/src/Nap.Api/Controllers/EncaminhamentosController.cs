using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Comum;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Encaminhamentos.Commands;
using Nap.Application.Features.Encaminhamentos.Queries;
using Nap.Domain.Encaminhamentos;

namespace Nap.Api.Controllers;

[ApiController]
[Route("api/encaminhamentos")]
[Authorize(Policy = Politicas.PodeConsultarEncaminhamentos)]
public class EncaminhamentosController : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<EncaminhamentoListaDto>> Listar([FromServices] ListarEncaminhamentosHandler handler, Guid? alunoId,
        Guid? cursoId, Especialidade? especialidade, DestinoEncaminhamento? destino, EstadoEncaminhamento? estado, DateOnly? de,
        DateOnly? ate, bool? semAcolhimentoNoPrazo, CancellationToken ct, int pagina = 1, int tamanho = 20) =>
        handler.Handle(new ListarEncaminhamentosQuery(alunoId, cursoId, especialidade, destino, estado, de, ate, semAcolhimentoNoPrazo,
            pagina, tamanho), ct);

    [HttpGet("{id:guid}")]
    public Task<EncaminhamentoDetalheDto> Obter(Guid id, [FromServices] ObterEncaminhamentoHandler handler, CancellationToken ct) =>
        handler.Handle(new ObterEncaminhamentoQuery(id), ct);

    [HttpPost]
    [Authorize(Policy = Politicas.EquipeNap)]
    [Idempotente(nameof(Obter))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Criar(CriarEncaminhamentoRequest request, [FromServices] CriarEncaminhamentoHandler handler,
        CancellationToken ct)
    {
        var id = await handler.Handle(new CriarEncaminhamentoCommand(request.AlunoId, request.Especialidade, request.Destino,
            request.Data, request.MotivoRestrito, request.ResumoCompartilhavel, request.Observacoes), ct);
        return CreatedAtAction(nameof(Obter), new { id }, new IdCriado(id));
    }

    [HttpPost("{id:guid}/retornos")]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RegistrarRetorno(Guid id, RegistrarRetornoRequest request,
        [FromServices] RegistrarRetornoEncaminhamentoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new RegistrarRetornoEncaminhamentoCommand(id, request.Acao, request.Data, request.Texto,
            HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancelar(Guid id, CancelarEncaminhamentoRequest request,
        [FromServices] CancelarEncaminhamentoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new CancelarEncaminhamentoCommand(id, request.Justificativa, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }
}
