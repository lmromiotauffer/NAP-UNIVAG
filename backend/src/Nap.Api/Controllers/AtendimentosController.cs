using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Comum;
using Nap.Application.Features.Atendimentos;
using Nap.Application.Features.Atendimentos.Commands;
using Nap.Application.Features.Atendimentos.Queries;
using Nap.Domain.Atendimentos;

namespace Nap.Api.Controllers;

[ApiController]
[Route("api/atendimentos")]
[Authorize(Policy = Politicas.EquipeNap)]
public class AtendimentosController : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AtendimentoListaDto>> Listar([FromServices] ListarAtendimentosHandler handler, Guid? alunoId,
        Guid? profissionalId, EstadoAtendimento? estado, TipoAtendimento? tipo, DateOnly? de, DateOnly? ate, CancellationToken ct,
        int pagina = 1, int tamanho = 20) =>
        handler.Handle(new ListarAtendimentosQuery(alunoId, profissionalId, estado, tipo, de, ate, pagina, tamanho), ct);

    [HttpGet("{id:guid}")]
    public Task<AtendimentoDetalheDto> Obter(Guid id, [FromServices] ObterAtendimentoHandler handler, CancellationToken ct) =>
        handler.Handle(new ObterAtendimentoQuery(id), ct);

    [HttpPost]
    [Idempotente(nameof(Obter))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Registrar(RegistrarAtendimentoRequest request, [FromServices] RegistrarAtendimentoHandler handler,
        CancellationToken ct)
    {
        var id = await handler.Handle(new RegistrarAtendimentoCommand(request.CompromissoId, request.AlunoId, request.ProfissionalId,
            request.Inicio, request.Fim, request.Tipo, request.Estado, request.DemandaPrincipal, request.ObservacoesPrivadas,
            request.RecomendacoesEncaminhamento, request.ProximosPassos, request.JustificativaAvulso), ct);
        return CreatedAtAction(nameof(Obter), new { id }, new IdCriado(id));
    }

    [HttpPut("{id:guid}")]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Corrigir(Guid id, CorrigirAtendimentoRequest request, [FromServices] CorrigirAtendimentoHandler handler,
        CancellationToken ct)
    {
        await handler.Handle(new CorrigirAtendimentoCommand(id, request.Estado, request.Tipo, request.Inicio, request.Fim,
            request.DemandaPrincipal, request.ObservacoesPrivadas, request.RecomendacoesEncaminhamento, request.ProximosPassos,
            request.Justificativa, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }
}
