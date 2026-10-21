using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Contratos;
using Nap.Api.Infra;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Agenda.Commands;
using Nap.Application.Features.Agenda.Queries;
using Nap.Application.Features.Producao.Commands;

namespace Nap.Api.Controllers;

/// <summary>Não há <c>GET /agenda/{id}</c> na Fase 1: o Location das criações aponta para a semana do compromisso.</summary>
[ApiController]
[Route("api/agenda")]
[Authorize(Policy = Politicas.EquipeNap)]
public class AgendaController : ControllerBase
{
    /// <param name="inicio">Segunda-feira da semana (yyyy-MM-dd).</param>
    [HttpGet("semana")]
    public Task<AgendaSemanaDto> Semana(DateOnly inicio, Guid? profissionalId, [FromServices] ListarAgendaSemanaHandler handler,
        CancellationToken ct) =>
        handler.Handle(new ListarAgendaSemanaQuery(inicio, profissionalId), ct);

    /// <param name="semana">Segunda-feira da semana (yyyy-MM-dd). Sem <c>profissionalId</c>: escala padrão com os feriados.</param>
    [HttpGet("escala-semana")]
    public Task<EscalaSemanaDto> EscalaSemana(DateOnly semana, Guid? profissionalId, [FromServices] ObterEscalaSemanaHandler handler,
        CancellationToken ct) =>
        handler.Handle(new ObterEscalaSemanaQuery(semana, profissionalId), ct);

    [HttpPost]
    [Idempotente(nameof(Semana))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    [ProducesResponseType<ConflitoAgendaProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Criar(CriarCompromissoRequest request, [FromServices] CriarCompromissoHandler handler,
        [FromServices] IRelogio relogio, CancellationToken ct)
    {
        var id = await handler.Handle(new CriarCompromissoCommand(request.Tipo, request.Titulo, request.Inicio, request.Fim,
            request.ProfissionalId, request.AlunoId, request.Descricao, request.ParticipantesIds, request.TodaEquipe ?? false), ct);
        return CreatedAtAction(nameof(Semana), request.ValoresRota(relogio.Fuso), new IdCriado(id));
    }

    /// <param name="escopo"><c>Grupo</c>: todas as instâncias agendadas do compromisso da equipe (criador ou coordenação).</param>
    [HttpPut("{id:guid}")]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarCompromissoRequest request,
        [FromServices] AtualizarCompromissoHandler handler, CancellationToken ct, [FromQuery] EscopoGrupo escopo = EscopoGrupo.Minha)
    {
        await handler.Handle(new AtualizarCompromissoCommand(id, request.Titulo, request.Descricao, HttpContext.VersaoIfMatch(), escopo), ct);
        return NoContent();
    }

    /// <summary>Cria um novo compromisso (201 com o novo id) e marca o original como remarcado.</summary>
    /// <param name="escopo"><c>Grupo</c>: remarca todas as instâncias agendadas do compromisso da equipe (criador ou coordenação).</param>
    [HttpPost("{id:guid}/remarcar")]
    [ExigeIfMatch]
    [Idempotente(nameof(Semana))]
    [ProducesResponseType<IdCriado>(StatusCodes.Status201Created)]
    [ProducesResponseType<ConflitoAgendaProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remarcar(Guid id, RemarcarCompromissoRequest request,
        [FromServices] RemarcarCompromissoHandler handler, [FromServices] IRelogio relogio, CancellationToken ct,
        [FromQuery] EscopoGrupo escopo = EscopoGrupo.Minha)
    {
        var novoId = await handler.Handle(new RemarcarCompromissoCommand(id, request.Inicio, request.Fim, HttpContext.VersaoIfMatch(), escopo), ct);
        return CreatedAtAction(nameof(Semana), request.ValoresRota(relogio.Fuso), new IdCriado(novoId));
    }

    /// <param name="escopo"><c>Grupo</c>: cancela todas as instâncias agendadas do compromisso da equipe (criador ou coordenação).</param>
    [HttpPost("{id:guid}/cancelar")]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancelar(Guid id, CancelarCompromissoRequest request,
        [FromServices] CancelarCompromissoHandler handler, CancellationToken ct, [FromQuery] EscopoGrupo escopo = EscopoGrupo.Minha)
    {
        await handler.Handle(new CancelarCompromissoCommand(id, request.Motivo, HttpContext.VersaoIfMatch(), escopo), ct);
        return NoContent();
    }

    /// <summary>Registra a realização (horário efetivo) de reunião, PEI ou tarefa administrativa depois do término.</summary>
    [HttpPost("{id:guid}/realizacao")]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RegistrarRealizacao(Guid id, RegistrarRealizacaoRequest request,
        [FromServices] RegistrarRealizacaoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new RegistrarRealizacaoCommand(id, HttpContext.VersaoIfMatch(), request.Inicio, request.Fim), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/realizacao")]
    [ExigeIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CorrigirRealizacao(Guid id, CorrigirRealizacaoRequest request,
        [FromServices] CorrigirRealizacaoHandler handler, CancellationToken ct)
    {
        await handler.Handle(new CorrigirRealizacaoCommand(id, HttpContext.VersaoIfMatch(), request.Inicio, request.Fim,
            request.Justificativa), ct);
        return NoContent();
    }
}
