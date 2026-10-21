using Microsoft.AspNetCore.Mvc;
using Nap.Application.Features.Agenda;

namespace Nap.Api.Infra;

/// <summary>Forma do 409 de bloqueio sobre horário ocupado (extensão <c>conflitos</c>); só documenta o contrato no OpenAPI.</summary>
public class ConflitoAgendaProblemDetails : ProblemDetails
{
    public IReadOnlyList<ConflitoAgendaDto> Conflitos { get; set; } = [];
}
