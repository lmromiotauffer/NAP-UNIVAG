namespace Nap.Domain.Comum;

public static class Perfis
{
    public const string Administrador = "Administrador";
    public const string CoordenadorNap = "CoordenadorNap";
    public const string ProfissionalNap = "ProfissionalNap";
    public const string CoordenadorCurso = "CoordenadorCurso";
    public const string Sip = "Sip";
    public const string Gestao = "Gestao";

    public static IReadOnlySet<string> Todos { get; } = new HashSet<string>
    {
        Administrador, CoordenadorNap, ProfissionalNap, CoordenadorCurso, Sip, Gestao,
    };

    public static IReadOnlySet<string> EquipeNap { get; } = new HashSet<string>
    {
        Administrador, CoordenadorNap, ProfissionalNap,
    };
}
