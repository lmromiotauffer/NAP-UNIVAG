using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Comum;
using Nap.Application.Features.Comunicacoes;
using Nap.Application.Features.Comunicacoes.Commands;
using Nap.Application.Features.Comunicacoes.Queries;
using Nap.Application.Features.Notificacoes;

namespace Nap.Api.Controllers;

[ApiController]
[Route("api/comunicacoes")]
[Authorize(Policy = Politicas.PodeUsarComunicacoes)]
public class ComunicacoesController : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<MensagemListaDto>> Listar([FromServices] ListarCaixaHandler handler, CaixaMensagens caixa, string? busca,
        Guid? alunoId, Guid? encaminhamentoId, CancellationToken ct, int pagina = 1, int tamanho = 20) =>
        handler.Handle(new ListarCaixaQuery(caixa, busca, alunoId, pagina, tamanho, encaminhamentoId), ct);

    [HttpGet("{id:guid}")]
    public Task<MensagemDetalheDto> Obter(Guid id, [FromServices] ObterMensagemHandler handler, CancellationToken ct) =>
        handler.Handle(new ObterMensagemQuery(id), ct);

    [HttpGet("contagem")]
    public Task<ContagemNaoLidasDto> Contar([FromServices] ContarMensagensNaoLidasHandler handler, CancellationToken ct) =>
        handler.Handle(ct);

    [HttpGet("destinatarios")]
    public Task<IReadOnlyList<DestinatarioDto>> ListarDestinatarios([FromServices] ListarDestinatariosHandler handler, Guid? alunoId,
        Guid? encaminhamentoId, Guid? cursoId, CancellationToken ct) =>
        handler.Handle(new ListarDestinatariosQuery(alunoId, encaminhamentoId, cursoId), ct);

    [HttpPost("rascunhos")]
    [Idempotente(nameof(Obter))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CriarRascunho(RascunhoRequest request, [FromServices] CriarRascunhoHandler handler,
        CancellationToken ct)
    {
        var id = await handler.Handle(new CriarRascunhoCommand(request.Tipo, request.Assunto, request.Corpo, request.DestinatarioId,
            request.CursoId, request.AlunoId, request.EncaminhamentoId), ct);
        return CreatedAtAction(nameof(Obter), new { id }, new IdCriado(id));
    }

    [HttpPut("rascunhos/{id:guid}")]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarRascunho(Guid id, RascunhoRequest request,
        [FromServices] AtualizarRascunhoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new AtualizarRascunhoCommand(id, request.Tipo, request.Assunto, request.Corpo, request.DestinatarioId,
            request.CursoId, request.AlunoId, request.EncaminhamentoId, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpDelete("rascunhos/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DescartarRascunho(Guid id, [FromServices] DescartarRascunhoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new DescartarRascunhoCommand(id), ct);
        return NoContent();
    }

    /// <summary>Com <c>rascunhoId</c>, o <c>If-Match</c> é opcional: presente, recusa rascunho alterado por outra aba (409).</summary>
    [HttpPost]
    [Idempotente(nameof(Obter))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Enviar(EnviarMensagemRequest request, [FromServices] EnviarMensagemHandler handler,
        CancellationToken ct)
    {
        var versao = HttpContext.VersaoIfMatch();
        var id = await handler.Handle(new EnviarMensagemCommand(request.RascunhoId, request.DestinatarioId, request.Tipo,
            request.Assunto, request.Corpo, request.CursoId, request.AlunoId, request.EncaminhamentoId,
            versao.Length > 0 ? versao : null), ct);
        return CreatedAtAction(nameof(Obter), new { id }, new IdCriado(id));
    }

    [HttpPost("{id:guid}/respostas")]
    [Idempotente(nameof(Obter))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Responder(Guid id, ResponderMensagemRequest request,
        [FromServices] ResponderMensagemHandler handler, CancellationToken ct)
    {
        var novoId = await handler.Handle(new ResponderMensagemCommand(id, request.Corpo), ct);
        return CreatedAtAction(nameof(Obter), new { id = novoId }, new IdCriado(novoId));
    }

    [HttpPost("{id:guid}/lida")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarcarLida(Guid id, [FromServices] MarcarMensagemLidaHandler handler, CancellationToken ct)
    {
        await handler.Handle(new MarcarMensagemLidaCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/arquivar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Arquivar(Guid id, [FromServices] ArquivarMensagemHandler handler, CancellationToken ct)
    {
        await handler.Handle(new ArquivarMensagemCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/desarquivar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Desarquivar(Guid id, [FromServices] DesarquivarMensagemHandler handler, CancellationToken ct)
    {
        await handler.Handle(new DesarquivarMensagemCommand(id), ct);
        return NoContent();
    }
}
