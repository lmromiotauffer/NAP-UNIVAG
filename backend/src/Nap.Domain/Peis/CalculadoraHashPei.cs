using System.Globalization;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Nap.Domain.Peis;

// Contrato de evidência: mudar o formato invalida os hashes já assinados.
// SHA-256 (hex minúsculo) dos bytes UTF-8 de um objeto JSON compacto (System.Text.Json, JavaScriptEncoder.Default explícito),
// nesta ordem fixa: necessidadesEducacionais, adaptacoes, orientacoesProfessores, recursosApoio,
// cuidadosPedagogicos (string ou null explícito, já aparados), vigenciaInicio, vigenciaFim ("yyyy-MM-dd").
// DemandaInterna não entra: é de uso interno do NAP e não faz parte do documento assinado.
public static class CalculadoraHashPei
{
    public static string Calcular(ConteudoPei c)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
        {
            json.WriteStartObject();
            json.WriteString("necessidadesEducacionais", c.NecessidadesEducacionais);
            json.WriteString("adaptacoes", c.Adaptacoes);
            json.WriteString("orientacoesProfessores", c.OrientacoesProfessores);
            json.WriteString("recursosApoio", c.RecursosApoio);
            json.WriteString("cuidadosPedagogicos", c.CuidadosPedagogicos);
            json.WriteString("vigenciaInicio", c.Vigencia.Inicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            json.WriteString("vigenciaFim", c.Vigencia.Fim.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            json.WriteEndObject();
        }
        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }
}
