using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Application.Comum;
using Nap.Application.Features.Notificacoes;
using Nap.Application.Features.Notificacoes.Commands;
using Nap.Application.Features.Notificacoes.Queries;

namespace Nap.Api.Controllers;

/// <summary>Sempre só as notificações do usuário autenticado, qualquer que seja o perfil.</summary>
[ApiController]
[Route("api/notificacoes")]
[Authorize]
public class NotificacoesController : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<NotificacaoDto>> Listar([FromServices] ListarNotificacoesHandler handler, CancellationToken ct,
        bool naoLidas = false, int pagina = 1, int tamanho = 20) =>
        handler.Handle(new ListarNotificacoesQuery(naoLidas, pagina, tamanho), ct);

    [HttpGet("contagem")]
    public Task<ContagemNaoLidasDto> Contagem([FromServices] ContarNaoLidasHandler handler, CancellationToken ct) => handler.Handle(ct);

    [HttpPost("{id:guid}/lida")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarcarLida(Guid id, [FromServices] MarcarNotificacaoLidaHandler handler, CancellationToken ct)
    {
        await handler.Handle(new MarcarNotificacaoLidaCommand(id), ct);
        return NoContent();
    }

    [HttpPost("lidas")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarcarTodasLidas([FromServices] MarcarTodasLidasHandler handler, CancellationToken ct)
    {
        await handler.Handle(ct);
        return NoContent();
    }
}
