using System.Text.RegularExpressions;

namespace Nap.Domain.Comum.ValueObjects;

public sealed partial record Matricula
{
    public string Valor { get; }

    private Matricula(string valor) => Valor = valor;

    public static Matricula Criar(string valor)
    {
        var normalizado = (valor ?? string.Empty).Trim().ToUpperInvariant();
        if (!PadraoValido().IsMatch(normalizado))
            throw new DomainException("A matrícula deve ter de 3 a 20 caracteres (letras, números, ponto ou hífen).");
        return new Matricula(normalizado);
    }

    [GeneratedRegex("^[A-Z0-9.-]{3,20}$")]
    private static partial Regex PadraoValido();
}
