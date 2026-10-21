using System.Text.Json;
using FluentValidation;

namespace Nap.Application.Comum;

public static class Validacao
{
    public static async Task ValidarAsync<T>(this IValidator<T> validador, T entrada, CancellationToken ct)
    {
        var resultado = await validador.ValidateAsync(entrada, ct);
        if (resultado.IsValid) return;
        var erros = resultado.Errors
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        throw new ValidationException(erros);
    }

    /// <summary>Erro de entrada em um campo específico (campo em camelCase).</summary>
    public static ValidationException ErroDeCampo(string campo, string mensagem) =>
        new(new Dictionary<string, string[]> { [campo] = [mensagem] });
}
