using Nap.Application.Comum;

namespace Nap.Application.Features.Peis;

public enum VariantePdf { Completa, Pedagogica }

/// <summary>Conteúdo do PDF do PEI para um leitor; a variante <see cref="VariantePdf.Pedagogica"/> nunca leva a demanda interna.</summary>
public record ModeloPdfPei(VariantePdf Variante, string Aluno, string Matricula, string Curso, string Responsavel, int Numero, string Estado,
    DateOnly VigenciaInicio, DateOnly VigenciaFim, IReadOnlyList<(string Titulo, string Texto)> Secoes,
    IReadOnlyList<AssinaturaDto> Assinaturas, string? HashConteudo, DateTimeOffset? PublicadaEm, IReadOnlyList<CienciaDto> Ciencias)
{
    public const string TituloDemandaInterna = "Demanda interna (Uso interno do NAP — não faz parte do documento assinado)";

    public string AvisoAssinatura => AcessoAluno.AvisoAssinatura;

    /// <summary>Método gravado pela assinatura demonstrativa; só ele leva o aviso de "não certificada".</summary>
    public const string MetodoDemonstrativo = "Demonstrativa";

    public static ModeloPdfPei Montar(VersaoPeiDetalheDto d, VariantePdf v)
    {
        var c = d.Conteudo;
        List<(string Titulo, string Texto)> secoes =
        [
            ("Necessidades educacionais", Texto(c.NecessidadesEducacionais)),
            ("Adaptações", Texto(c.Adaptacoes)),
            ("Orientações aos professores", Texto(c.OrientacoesProfessores)),
            ("Recursos de apoio", Texto(c.RecursosApoio)),
            ("Cuidados pedagógicos", Texto(c.CuidadosPedagogicos)),
        ];
        // Por último: fica separada do conteúdo assinado. Decidida pela variante, não só pelo DTO já recortado.
        if (v == VariantePdf.Completa) secoes.Add((TituloDemandaInterna, Texto(c.DemandaInterna)));
        return new(v, d.Aluno, d.Matricula, d.Curso, d.Responsavel, d.Numero, Rotulos.De(d.Estado), c.VigenciaInicio, c.VigenciaFim, secoes,
            d.Assinaturas, d.HashConteudo, d.PublicadaEm, d.Ciencias);
    }

    private static string Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? "Não informado." : valor;
}
