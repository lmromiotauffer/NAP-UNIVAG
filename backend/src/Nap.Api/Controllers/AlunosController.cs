using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Alunos.Commands;
using Nap.Application.Features.Alunos.Queries;
using Nap.Domain.Alunos;
using Nap.Domain.Encaminhamentos;

namespace Nap.Api.Controllers;

[ApiController]
[Route("api/alunos")]
[Authorize(Policy = Politicas.PodeConsultarAlunos)]
public class AlunosController : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AlunoListaDto>> Listar([FromServices] ListarAlunosHandler handler, string? busca, Guid? cursoId,
        Guid? profissionalId, SituacaoAluno? situacao, DateOnly? inicioDe, DateOnly? inicioAte, Especialidade? especialidadeEncaminhamento,
        CancellationToken ct, int pagina = 1, int tamanho = 20) =>
        handler.Handle(new ListarAlunosQuery(busca, cursoId, profissionalId, situacao, inicioDe, inicioAte, pagina, tamanho, especialidadeEncaminhamento), ct);

    [HttpGet("{id:guid}")]
    public Task<AlunoFichaDto> Obter(Guid id, [FromServices] ObterAlunoHandler handler, CancellationToken ct) =>
        handler.Handle(new ObterAlunoQuery(id), ct);

    [HttpGet("{id:guid}/linha-do-tempo")]
    public Task<PagedResult<EventoLinhaDoTempoDto>> LinhaDoTempo(Guid id, [FromServices] ListarLinhaDoTempoHandler handler,
        CancellationToken ct, int pagina = 1, int tamanho = 20) =>
        handler.Handle(new ListarLinhaDoTempoQuery(id, pagina, tamanho), ct);

    [HttpPost]
    [Authorize(Policy = Politicas.EquipeNap)]
    [Idempotente(nameof(Obter))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Criar(CriarAlunoRequest request, [FromServices] CriarAlunoHandler handler, CancellationToken ct)
    {
        var id = await handler.Handle(new CriarAlunoCommand(request.NomeCompleto, request.Matricula, request.CursoId, request.Semestre,
            request.InicioAcompanhamento, request.ProfissionalResponsavelId, request.NecessidadesEducacionais,
            request.ObservacoesAcademicas), ct);
        return CreatedAtAction(nameof(Obter), new { id }, new IdCriado(id));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAlunoRequest request, [FromServices] AtualizarAlunoHandler handler,
        CancellationToken ct)
    {
        await handler.Handle(new AtualizarAlunoCommand(id, request.NomeCompleto, request.Semestre, request.NecessidadesEducacionais,
            request.ObservacoesAcademicas, request.ProfissionalResponsavelId, request.MatriculaAtiva, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/situacao")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AlterarSituacao(Guid id, AlterarSituacaoAlunoRequest request,
        [FromServices] AlterarSituacaoAlunoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new AlterarSituacaoAlunoCommand(id, request.Situacao, request.Motivo, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/transferencia")]
    [Authorize(Policy = Politicas.EquipeNap)]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Transferir(Guid id, TransferirCursoAlunoRequest request,
        [FromServices] TransferirCursoAlunoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new TransferirCursoAlunoCommand(id, request.NovoCursoId, request.Data, request.Motivo, HttpContext.VersaoIfMatch()), ct);
        return NoContent();
    }
}
