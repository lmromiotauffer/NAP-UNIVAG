using Nap.Domain.Comum;

namespace Nap.Domain.Configuracoes;

public class ConfiguracaoAlertas : Entidade
{
    public int FaltasConsecutivas { get; private set; }
    public int FaltasJanela { get; private set; }
    public int JanelaDias { get; private set; }
    public int DiasSemAcolhimento { get; private set; }
    public int DiasSemAtualizacao { get; private set; }
    public int DiasPeiPendente { get; private set; }

    private ConfiguracaoAlertas() { } // ORM

    public static ConfiguracaoAlertas Padrao() => new()
    {
        FaltasConsecutivas = 3, FaltasJanela = 3, JanelaDias = 30,
        DiasSemAcolhimento = 7, DiasSemAtualizacao = 15, DiasPeiPendente = 15,
    };

    public void Atualizar(int faltasConsecutivas, int faltasJanela, int janelaDias, int diasSemAcolhimento,
        int diasSemAtualizacao, int diasPeiPendente)
    {
        Exigir(faltasConsecutivas, 1, 20, "Faltas consecutivas");
        Exigir(faltasJanela, 1, 50, "Faltas na janela");
        Exigir(janelaDias, 7, 365, "Janela em dias");
        Exigir(diasSemAcolhimento, 1, 90, "Dias sem acolhimento");
        Exigir(diasSemAtualizacao, 1, 180, "Dias sem atualização");
        Exigir(diasPeiPendente, 1, 180, "Dias de PEI pendente");
        FaltasConsecutivas = faltasConsecutivas;
        FaltasJanela = faltasJanela;
        JanelaDias = janelaDias;
        DiasSemAcolhimento = diasSemAcolhimento;
        DiasSemAtualizacao = diasSemAtualizacao;
        DiasPeiPendente = diasPeiPendente;
    }

    private static void Exigir(int valor, int min, int max, string campo)
    {
        if (valor < min || valor > max)
            throw new DomainException($"{campo} deve estar entre {min} e {max}.");
    }
}
