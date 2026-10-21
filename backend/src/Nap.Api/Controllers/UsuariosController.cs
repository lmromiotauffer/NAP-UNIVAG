using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;
using Nap.Application.Features.Usuarios.Commands;
using Nap.Application.Features.Usuarios.Queries;
using Nap.Domain.Comum;

namespace Nap.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize]
public class UsuariosController : ControllerBase
{
    /// <summary>Profissionais ativos (responsáveis, filtros de agenda e painel). <paramref name="incluirInativos"/> só vale para
    /// coordenação e gestão (filtro da produção); os demais perfis recebem só os ativos.</summary>
    [HttpGet("profissionais")]
    public Task<IReadOnlyList<ProfissionalDto>> Profissionais([FromServices] IUsuarioQueries usuarios, [FromServices] IUsuarioAtual usuario,
        CancellationToken ct, bool incluirInativos = false) =>
        usuarios.ListarProfissionaisAsync(incluirInativos && (usuario.EhCoordenacao() || usuario.Perfil == Perfis.Gestao), ct);

    [HttpGet]
    [Authorize(Policy = Politicas.PodeAdministrar)]
    public Task<PagedResult<UsuarioAdminDto>> Listar(string? busca, string? perfil, Guid? cursoId, bool? ativo,
        [FromServices] ListarUsuariosHandler handler, CancellationToken ct, int pagina = 1, int tamanho = 20) =>
        handler.Handle(new ListarUsuariosQuery(busca, perfil, cursoId, ativo, pagina, tamanho), ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Politicas.PodeAdministrar)]
    public Task<UsuarioAdminDto> Obter(Guid id, [FromServices] ObterUsuarioHandler handler, CancellationToken ct) =>
        handler.Handle(new ObterUsuarioQuery(id), ct);

    /// <summary>A senha temporária aparece só nesta resposta: <c>no-store</c> e sem <c>Idempotente</c>, cujo cache guardaria o corpo.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.PodeAdministrar)]
    [SemRastro]
    [ProducesResponseType<SenhaTemporariaDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Criar(CriarUsuarioRequest request, [FromServices] CriarUsuarioHandler handler, CancellationToken ct)
    {
        var criado = await handler.Handle(new CriarUsuarioCommand(request.Email, request.NomeCompleto, request.Perfil, request.CursoId), ct);
        return CreatedAtAction(nameof(Obter), new { id = criado.UsuarioId }, criado);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Politicas.PodeAdministrar)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarUsuarioRequest request, [FromServices] AtualizarUsuarioHandler handler,
        CancellationToken ct)
    {
        await handler.Handle(new AtualizarUsuarioCommand(id, request.NomeCompleto, request.Perfil, request.CursoId, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/situacao")]
    [Authorize(Policy = Politicas.PodeAdministrar)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AlterarSituacao(Guid id, AlterarSituacaoRequest request,
        [FromServices] AlterarSituacaoUsuarioHandler handler, CancellationToken ct)
    {
        await handler.Handle(new AlterarSituacaoUsuarioCommand(id, request.Ativo, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/senha-temporaria")]
    [Authorize(Policy = Politicas.PodeAdministrar)]
    [SemRastro]
    public Task<SenhaTemporariaDto> RedefinirSenha(Guid id, [FromServices] RedefinirSenhaUsuarioHandler handler, CancellationToken ct) =>
        handler.Handle(new RedefinirSenhaUsuarioCommand(id), ct);
}
