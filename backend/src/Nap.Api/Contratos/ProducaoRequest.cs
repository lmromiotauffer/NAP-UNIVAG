using System.Globalization;
using Nap.Domain.Producao;

namespace Nap.Api.Contratos;

public record FecharSemanaRequest(Guid ProfissionalId, DateOnly Semana, bool ConfirmarPendencias);

/// <summary>A versão do fechamento vem do cabeçalho <c>If-Match</c>.</summary>
public record ReabrirSemanaRequest(Guid ProfissionalId, DateOnly Semana, string Justificativa);

/// <summary>Criação cujo Location é a listagem do dia do ajuste para o profissional.</summary>
public record CriarAjusteRequest(Guid ProfissionalId, DateOnly Data, CategoriaProducao Categoria, int? Ocorrencias, int? Minutos,
    string Justificativa) : IComLocalCriacao
{
    public object ValoresRota(TimeZoneInfo fuso)
    {
        var dia = Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new { profissionalId = ProfissionalId, inicio = dia, fim = dia };
    }
}

