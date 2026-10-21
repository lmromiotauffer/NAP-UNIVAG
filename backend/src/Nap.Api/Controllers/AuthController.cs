using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Comum;
using Nap.Application.Features.Auth;

namespace Nap.Api.Controllers;

/// <summary>
/// Sessão. Login, refresh e logout são anônimos (refresh/logout se identificam pelo cookie). Refresh e logout exigem
/// <c>X-Requested-With: nap-spa</c>. Sem <c>EmTransacaoAsync</c>: o ServicoSessao controla a própria transação.
/// </summary>
[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private const string SemCabecalhoSpa = "Cabeçalho X-Requested-With: nap-spa obrigatório.";

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LimiteLogin.Politica)]
    public async Task<ActionResult<SessaoDto>> Login(LoginRequest request, [FromServices] IServicoSessao sessao, CancellationToken ct) =>
        Emitir(await sessao.EntrarAsync(request.Email, request.Senha, ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<SessaoDto>> Refresh([FromServices] IServicoSessao sessao, CancellationToken ct)
    {
        if (!CookieRefresh.CabecalhoSpaPresente(Request)) return Problem(statusCode: 400, title: SemCabecalhoSpa);
        var token = CookieRefresh.Ler(Request) ?? throw new UnauthorizedException();
        try
        {
            return Emitir(await sessao.RenovarAsync(token, ct));
        }
        catch (UnauthorizedException e)
        {
            // Resposta direta (não relança): o middleware de exceção limparia o Set-Cookie que apaga o token morto.
            CookieRefresh.Remover(Response);
            return Problem(statusCode: 401, title: e.Message);
        }
    }

    /// <summary>Anônimo: funciona só com o cookie (access token vencido ou ausente).</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromServices] IServicoSessao sessao, CancellationToken ct)
    {
        if (!CookieRefresh.CabecalhoSpaPresente(Request)) return Problem(statusCode: 400, title: SemCabecalhoSpa);
        if (CookieRefresh.Ler(Request) is { } token)
            await sessao.EncerrarAsync(token, ct);
        CookieRefresh.Remover(Response);
        return NoContent();
    }

    [HttpGet("eu")]
    public Task<UsuarioSessaoDto> Eu([FromServices] IServicoSessao sessao, [FromServices] IUsuarioAtual usuario, CancellationToken ct) =>
        sessao.ObterUsuarioAsync(usuario.Id, ct);

    /// <summary>Troca da própria senha (liberada mesmo com a troca pendente). Encerra as outras sessões e emite uma nova.</summary>
    [HttpPost("senha")]
    public async Task<ActionResult<SessaoDto>> Senha(TrocarSenhaRequest request, [FromServices] IServicoSessao sessao,
        [FromServices] IUsuarioAtual usuario, CancellationToken ct) =>
        Emitir(await sessao.TrocarSenhaAsync(usuario.Id, request.SenhaAtual, request.NovaSenha, ct));

    private SessaoDto Emitir(SessaoEmitida emitida)
    {
        CookieRefresh.Gravar(Response, emitida.RefreshToken, emitida.RefreshExpiraEm);
        return new SessaoDto(emitida.AccessToken, emitida.Usuario);
    }
}
