using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Features.Configuracoes;
using Nap.Application.Features.Configuracoes.Commands;
using Nap.Application.Features.Configuracoes.Queries;

namespace Nap.Api.Controllers;

[ApiController]
[Route("api/configuracoes")]
[Authorize(Policy = Politicas.PodeAdministrar)]
public class ConfiguracoesController : ControllerBase
{
    [HttpGet("alertas")]
    public Task<ConfiguracaoAlertasDto> ObterAlertas([FromServices] ObterConfiguracaoAlertasHandler handler, CancellationToken ct) =>
        handler.Handle(ct);

    /// <summary>Devolve a configuração salva, com a nova versão.</summary>
    [HttpPut("alertas")]
    [ExigeIfMatch]
    public Task<ConfiguracaoAlertasDto> AtualizarAlertas(AtualizarConfiguracaoAlertasRequest request,
        [FromServices] AtualizarConfiguracaoAlertasHandler handler, CancellationToken ct) =>
        handler.Handle(new AtualizarConfiguracaoAlertasCommand(request.FaltasConsecutivas, request.FaltasJanela, request.JanelaDias,
            request.DiasSemAcolhimento, request.DiasSemAtualizacao, request.DiasPeiPendente, HttpContext.VersaoIfMatch()), ct);
}
