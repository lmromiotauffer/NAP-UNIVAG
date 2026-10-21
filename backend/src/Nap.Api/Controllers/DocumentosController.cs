using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Comum;
using Nap.Application.Features.Documentos;
using Nap.Application.Features.Documentos.Commands;
using Nap.Application.Features.Documentos.Queries;
using Nap.Domain.Documentos;

namespace Nap.Api.Controllers;

[ApiController]
[Route("api/documentos")]
[Authorize(Policy = Politicas.PodeConsultarAlunos)]
public class DocumentosController : ControllerBase
{
    /// <summary>Teto do servidor para o corpo multipart; o limite de negócio (5 MiB) é <c>Nap:Arquivos:LimiteBytes</c>.</summary>
    private const int TetoCorpoBytes = 6 * 1024 * 1024;

    [HttpGet("/api/alunos/{alunoId:guid}/documentos")]
    public Task<PagedResult<DocumentoDto>> Listar(Guid alunoId, [FromServices] ListarDocumentosHandler handler, CancellationToken ct,
        bool incluirArquivados = false, int pagina = 1, int tamanho = 20) =>
        handler.Handle(new ListarDocumentosQuery(alunoId, incluirArquivados, pagina, tamanho), ct);

    [HttpGet("{id:guid}")]
    public Task<DocumentoDto> Obter(Guid id, [FromServices] ObterDocumentoHandler handler, CancellationToken ct,
        bool incluirArquivados = false) =>
        handler.Handle(new ObterDocumentoQuery(id, incluirArquivados), ct);

    [HttpPost("/api/alunos/{alunoId:guid}/documentos")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [Idempotente(nameof(Obter))]
    [RequestSizeLimit(TetoCorpoBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = TetoCorpoBytes)]
    [LeFormulario(TetoCorpoBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Anexar(Guid alunoId, IFormFile? arquivo, [FromForm] CategoriaDocumento? categoria,
        [FromForm] string? descricao, [FromServices] AnexarDocumentoHandler handler, CancellationToken ct)
    {
        // Anulável: ausente no formulário vira erro, não Laudo (valor padrão do enum).
        var categoriaInformada = categoria ?? throw Validacao.ErroDeCampo("categoria", "Informe a categoria.");
        await using var conteudo = arquivo?.OpenReadStream();
        var id = await handler.Handle(new AnexarDocumentoCommand(alunoId, categoriaInformada, descricao ?? "", arquivo?.FileName ?? "",
            conteudo!), ct);
        return CreatedAtAction(nameof(Obter), new { id }, new IdCriado(id));
    }

    /// <summary>Sempre anexo, com o tipo gravado no upload e sem sniffing: o conteúdo nunca é renderizado pela origem da Api.</summary>
    [HttpGet("{id:guid}/arquivo")]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, "application/octet-stream")]
    public async Task<IActionResult> Baixar(Guid id, [FromServices] BaixarDocumentoHandler handler, CancellationToken ct,
        bool incluirArquivados = false)
    {
        var arquivo = await handler.Handle(new BaixarDocumentoQuery(id, incluirArquivados), ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(arquivo.Conteudo, arquivo.TipoConteudo, arquivo.NomeArquivo, enableRangeProcessing: false);
    }

    [HttpPut("{id:guid}/categoria")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AlterarCategoria(Guid id, AlterarCategoriaDocumentoRequest request,
        [FromServices] AlterarCategoriaDocumentoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new AlterarCategoriaDocumentoCommand(id, request.Categoria, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/compartilhar")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Compartilhar(Guid id, JustificativaDocumentoRequest request,
        [FromServices] CompartilharDocumentoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new CompartilharDocumentoCommand(id, request.Justificativa, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/revogar-compartilhamento")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevogarCompartilhamento(Guid id, JustificativaDocumentoRequest request,
        [FromServices] RevogarCompartilhamentoDocumentoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new RevogarCompartilhamentoDocumentoCommand(id, request.Justificativa, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/arquivar")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Arquivar(Guid id, ArquivarDocumentoRequest request, [FromServices] ArquivarDocumentoHandler handler,
        CancellationToken ct)
    {
        await handler.Handle(new ArquivarDocumentoCommand(id, request.Motivo, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }
}
