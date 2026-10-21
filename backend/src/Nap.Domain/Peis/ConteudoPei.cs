using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Domain.Peis;

// A validação fica nos acessores init para valer tanto no construtor quanto em expressões "with".
public sealed record ConteudoPei
{
    public ConteudoPei(string? demandaInterna, string? necessidadesEducacionais, string? adaptacoes,
        string? orientacoesProfessores, string? recursosApoio, string? cuidadosPedagogicos, PeriodoVigencia vigencia)
    {
        DemandaInterna = demandaInterna;
        NecessidadesEducacionais = necessidadesEducacionais;
        Adaptacoes = adaptacoes;
        OrientacoesProfessores = orientacoesProfessores;
        RecursosApoio = recursosApoio;
        CuidadosPedagogicos = cuidadosPedagogicos;
        Vigencia = vigencia;
    }

    private ConteudoPei() { } // ORM: o EF não liga construtor com parâmetros em tipo complexo aninhado

    public string? DemandaInterna { get; init => field = Validar(value, "A demanda interna"); }
    public string? NecessidadesEducacionais { get; init => field = Validar(value, "As necessidades educacionais"); }
    public string? Adaptacoes { get; init => field = Validar(value, "As adaptações"); }
    public string? OrientacoesProfessores { get; init => field = Validar(value, "As orientações aos professores"); }
    public string? RecursosApoio { get; init => field = Validar(value, "Os recursos de apoio"); }
    public string? CuidadosPedagogicos { get; init => field = Validar(value, "Os cuidados pedagógicos"); }
    public PeriodoVigencia Vigencia { get; init => field = value ?? throw new DomainException("A vigência é obrigatória."); } = null!;

    public bool TemConteudoPedagogico =>
        PossuiConteudoPedagogico(NecessidadesEducacionais, Adaptacoes, OrientacoesProfessores, RecursosApoio, CuidadosPedagogicos);

    public static bool PossuiConteudoPedagogico(string? necessidades, string? adaptacoes, string? orientacoes, string? recursos,
        string? cuidados) =>
        necessidades is not null || adaptacoes is not null || orientacoes is not null || recursos is not null || cuidados is not null;

    private static string? Validar(string? texto, string nome)
    {
        texto = texto?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (texto.Length > 4000) throw new DomainException($"{nome} deve ter no máximo 4000 caracteres.");
        return texto;
    }
}
