namespace Nap.Domain.Comum;

public static class Texto
{
    /// <summary>Corta em <paramref name="maximo"/> caracteres sem partir um par substituto UTF-16 (emoji); o sufixo cabe no limite.</summary>
    public static string Truncar(string texto, int maximo, string sufixo = "")
    {
        if (texto.Length <= maximo) return texto;
        var corte = maximo - sufixo.Length;
        if (char.IsHighSurrogate(texto[corte - 1])) corte--;
        return texto[..corte] + sufixo;
    }

    /// <summary>Trim e tamanho entre <paramref name="minimo"/> e <paramref name="maximo"/>; <paramref name="nome"/> abre a mensagem ("O motivo").</summary>
    public static string Validar(string? texto, int minimo, int maximo, string nome)
    {
        texto = (texto ?? string.Empty).Trim();
        if (texto.Length < minimo || texto.Length > maximo)
            throw new DomainException($"{nome} deve ter de {minimo} a {maximo} caracteres.");
        return texto;
    }
}
