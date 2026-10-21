using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Application.Features.Painel;
using Nap.Application.Features.Painel.Queries;

namespace Nap.Api.Controllers;

[ApiController]
[Route("api/painel")]
[Authorize]
public class PainelController : ControllerBase
{
    /// <param name="semana">Segunda-feira da semana (yyyy-MM-dd); padrão: semana atual.</param>
    [HttpGet]
    public Task<PainelDto> Obter(DateOnly? semana, Guid? profissionalId, Guid? cursoId, [FromServices] ObterPainelHandler handler,
        CancellationToken ct) =>
        handler.Handle(new ObterPainelQuery(semana, profissionalId, cursoId), ct);
}
