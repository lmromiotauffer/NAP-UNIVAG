using System.Globalization;
using Nap.Application.Features.Producao;
using Nap.Domain.Agenda;

namespace Nap.Api.Contratos;

/// <summary>Criação cujo Location não é <c>/{id}</c>: o filtro de idempotência refaz o mesmo Location a partir do corpo.</summary>
public interface IComLocalCriacao
{
    object ValoresRota(TimeZoneInfo fuso);
}

/// <param name="ParticipantesIds">Compromisso da equipe (reuniões e lembrete): lista completa, incluindo quem cria (exceto coordenação).</param>
/// <param name="TodaEquipe">Compromisso da equipe com todos os profissionais ativos da equipe NAP.</param>
public record CriarCompromissoRequest(TipoCompromisso Tipo, string Titulo, DateTimeOffset Inicio, DateTimeOffset Fim,
    Guid? ProfissionalId, Guid? AlunoId, string? Descricao, IReadOnlyList<Guid>? ParticipantesIds = null, bool? TodaEquipe = null)
    : IComLocalCriacao
{
    public object ValoresRota(TimeZoneInfo fuso) => SemanaAgenda.Valores(Inicio, fuso);
}

public record AtualizarCompromissoRequest(string Titulo, string? Descricao);

public record RemarcarCompromissoRequest(DateTimeOffset Inicio, DateTimeOffset Fim) : IComLocalCriacao
{
    public object ValoresRota(TimeZoneInfo fuso) => SemanaAgenda.Valores(Inicio, fuso);
}

public record CancelarCompromissoRequest(string Motivo);

public record RegistrarRealizacaoRequest(DateTimeOffset Inicio, DateTimeOffset Fim);

public record CorrigirRealizacaoRequest(DateTimeOffset Inicio, DateTimeOffset Fim, string Justificativa);

internal static class SemanaAgenda
{
    /// <summary><c>inicio</c> = segunda-feira (no fuso institucional) da semana do compromisso, para <c>GET /agenda/semana</c>.</summary>
    public static object Valores(DateTimeOffset inicio, TimeZoneInfo fuso)
    {
        var dia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(inicio, fuso).DateTime);
        var segunda = ServicoFechamento.SegundaDe(dia);
        return new { inicio = segunda.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
    }
}
