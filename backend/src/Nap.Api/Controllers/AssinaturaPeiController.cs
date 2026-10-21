using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Features.Peis.Commands;
using Nap.Application.Features.Peis.Queries;

namespace Nap.Api.Controllers;

/// <summary>
/// Fluxo anônimo do aluno: o acesso temporário (token + código no corpo) substitui a autenticação. Não emite cookie; o
/// frontend não envia o <c>Authorization</c> da equipe para estas rotas.
/// </summary>
[ApiController]
[Route("api/assinar")]
[AllowAnonymous]
[SemRastro]
public class AssinaturaPeiController : ControllerBase
{
    [HttpPost("abrir")]
    public Task<VersaoParaAssinaturaDto> Abrir(AbrirAssinaturaRequest request, [FromServices] AbrirAcessoAssinaturaHandler handler,
        CancellationToken ct) =>
        handler.Handle(new AbrirAcessoAssinaturaQuery(request.Token, request.Codigo), ct);

    [HttpPost("assinar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Assinar(AssinarPeiAlunoRequest request, [FromServices] AssinarPeiAlunoHandler handler,
        CancellationToken ct)
    {
        await handler.Handle(new AssinarPeiAlunoCommand(request.Token, request.Codigo, request.NomeCompleto, request.Aceite), ct);
        return NoContent();
    }
}
