using System.Security.Cryptography;

namespace Nap.Application.Features.Usuarios;

/// <summary>16 caracteres sem ambíguos (0 O 1 l I), com ao menos uma maiúscula, uma minúscula e um dígito.</summary>
public static class GeradorSenhaTemporaria
{
    private const string Maiusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Minusculas = "abcdefghijkmnopqrstuvwxyz";
    private const string Digitos = "23456789";

    public static string Gerar()
    {
        char[] senha =
        [
            .. RandomNumberGenerator.GetItems<char>(Maiusculas, 1),
            .. RandomNumberGenerator.GetItems<char>(Minusculas, 1),
            .. RandomNumberGenerator.GetItems<char>(Digitos, 1),
            .. RandomNumberGenerator.GetItems<char>(Maiusculas + Minusculas + Digitos, 13),
        ];
        RandomNumberGenerator.Shuffle(senha.AsSpan());
        return new string(senha);
    }
}
