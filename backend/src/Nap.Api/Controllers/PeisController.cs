using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Features.Peis;
using Nap.Application.Features.Peis.Commands;
using Nap.Application.Features.Peis.Queries;

namespace Nap.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize(Policy = Politicas.PodeConsultarPeis)]
public class PeisController : ControllerBase
{
    [HttpGet("alunos/{alunoId:guid}/peis")]
    public Task<IReadOnlyList<PeiResumoDto>> Listar(Guid alunoId, [FromServices] ListarPeisDoAlunoHandler handler,
        CancellationToken ct) =>
        handler.Handle(new ListarPeisDoAlunoQuery(alunoId), ct);

    [HttpGet("peis/versoes/{id:guid}")]
    public Task<VersaoPeiDetalheDto> ObterVersao(Guid id, [FromServices] ObterVersaoPeiHandler handler, CancellationToken ct) =>
        handler.Handle(new ObterVersaoPeiQuery(id), ct);

    /// <summary>Cria o PEI com a versão 1 em rascunho; o id devolvido é o da versão.</summary>
    [HttpPost("alunos/{alunoId:guid}/peis")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [Idempotente(nameof(ObterVersao))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Criar(Guid alunoId, CriarPeiRequest request, [FromServices] CriarPeiHandler handler,
        CancellationToken ct)
    {
        var id = await handler.Handle(new CriarPeiCommand(alunoId, request.ProfissionalResponsavelId, request.Conteudo), ct);
        return CreatedAtAction(nameof(ObterVersao), new { id }, new IdCriado(id));
    }

    [HttpPost("peis/{peiId:guid}/versoes")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [Idempotente(nameof(ObterVersao))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CriarNovaVersao(Guid peiId, [FromServices] CriarNovaVersaoPeiHandler handler, CancellationToken ct)
    {
        var id = await handler.Handle(new CriarNovaVersaoPeiCommand(peiId), ct);
        return CreatedAtAction(nameof(ObterVersao), new { id }, new IdCriado(id));
    }

    [HttpPut("peis/versoes/{id:guid}/conteudo")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarConteudo(Guid id, ConteudoPeiInput conteudo,
        [FromServices] AtualizarConteudoPeiHandler handler, CancellationToken ct)
    {
        await handler.Handle(new AtualizarConteudoPeiCommand(id, conteudo, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    /// <summary>Só rascunho de versão posterior à primeira; exclusão física.</summary>
    [HttpDelete("peis/versoes/{id:guid}")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Descartar(Guid id, [FromServices] DescartarVersaoPeiHandler handler, CancellationToken ct)
    {
        await handler.Handle(new DescartarVersaoPeiCommand(id), ct);
        return NoContent();
    }

    [HttpPost("peis/versoes/{id:guid}/enviar-revisao")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EnviarParaRevisao(Guid id, [FromServices] EnviarPeiParaRevisaoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new EnviarPeiParaRevisaoCommand(id, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpPost("peis/versoes/{id:guid}/aprovar")]
    [Authorize(Policy = Politicas.PodePublicarPei)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Aprovar(Guid id, [FromServices] AprovarRevisaoPeiHandler handler, CancellationToken ct)
    {
        await handler.Handle(new AprovarRevisaoPeiCommand(id, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpPost("peis/versoes/{id:guid}/devolver")]
    [Authorize(Policy = Politicas.PodePublicarPei)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Devolver(Guid id, DevolverPeiRequest request, [FromServices] DevolverPeiHandler handler,
        CancellationToken ct)
    {
        await handler.Handle(new DevolverPeiCommand(id, request.Justificativa, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    /// <summary>Assinatura do profissional responsável (o handler recusa os demais com 403).</summary>
    [HttpPost("peis/versoes/{id:guid}/assinar")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Assinar(Guid id, [FromServices] AssinarPeiProfissionalHandler handler, CancellationToken ct)
    {
        await handler.Handle(new AssinarPeiProfissionalCommand(id, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    /// <summary>
    /// Token e código aparecem só nesta resposta: <c>no-store</c> e sem <c>Idempotente</c>, cujo cache guardaria o corpo.
    /// </summary>
    [HttpPost("peis/versoes/{id:guid}/acesso-aluno")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ProducesResponseType<AcessoGeradoDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> GerarAcessoAluno(Guid id, [FromServices] GerarAcessoAssinaturaAlunoHandler handler,
        CancellationToken ct)
    {
        var acesso = await handler.Handle(new GerarAcessoAssinaturaAlunoCommand(id), ct);
        Response.Headers.CacheControl = "no-store";
        return StatusCode(StatusCodes.Status201Created, acesso);
    }

    /// <summary>Troca o profissional responsável do PEI (histórico com justificativa); assinaturas já feitas permanecem.</summary>
    [HttpPost("peis/{id:guid}/responsavel")]
    [Authorize(Policy = Politicas.PodePublicarPei)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> TrocarResponsavel(Guid id, TrocarResponsavelPeiRequest request,
        [FromServices] TrocarResponsavelPeiHandler handler, CancellationToken ct)
    {
        await handler.Handle(new TrocarResponsavelPeiCommand(id, request.NovoResponsavelId, request.Justificativa), ct);
        return NoContent();
    }

    [HttpPost("peis/versoes/{id:guid}/publicar")]
    [Authorize(Policy = Politicas.PodePublicarPei)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Publicar(Guid id, [FromServices] PublicarPeiHandler handler, CancellationToken ct)
    {
        await handler.Handle(new PublicarPeiCommand(id, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpPost("peis/versoes/{id:guid}/arquivar")]
    [Authorize(Policy = Politicas.PodePublicarPei)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Arquivar(Guid id, ArquivarPeiRequest request, [FromServices] ArquivarPeiHandler handler,
        CancellationToken ct)
    {
        await handler.Handle(new ArquivarPeiCommand(id, request.Motivo, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    /// <summary>Só o coordenador do curso da publicação (o handler recusa os demais com 403); repetir não grava de novo.</summary>
    [HttpPost("peis/versoes/{id:guid}/ciencia")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RegistrarCiencia(Guid id, [FromServices] RegistrarCienciaPeiHandler handler, CancellationToken ct)
    {
        await handler.Handle(new RegistrarCienciaPeiCommand(id), ct);
        return NoContent();
    }

    /// <summary>Mesmos cabeçalhos do download de documentos: anexo, <c>filename*</c>, <c>nosniff</c>, sem <c>Range</c>.</summary>
    [HttpGet("peis/versoes/{id:guid}/pdf")]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> BaixarPdf(Guid id, [FromServices] BaixarPdfPeiHandler handler, CancellationToken ct)
    {
        var arquivo = await handler.Handle(new BaixarPdfPeiQuery(id), ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(arquivo.Conteudo, arquivo.TipoConteudo, arquivo.NomeArquivo, enableRangeProcessing: false);
    }
}
