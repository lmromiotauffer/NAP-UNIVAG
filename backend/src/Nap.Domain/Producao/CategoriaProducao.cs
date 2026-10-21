namespace Nap.Domain.Producao;

public enum CategoriaProducao
{
    AtendimentosRealizados,
    Ausencias,
    Ociosidade,
    ReunioesEquipe,
    ReunioesCoordenacao,
    ElaboracaoPei,
    Administrativo,
    AtividadesCursos,
}

public static class CategoriaProducaoExtensoes
{
    public static string Rotulo(this CategoriaProducao c) => c switch
    {
        CategoriaProducao.AtendimentosRealizados => "Atendimentos realizados",
        CategoriaProducao.Ausencias => "Ausências de alunos",
        CategoriaProducao.Ociosidade => "Horários disponíveis sem utilização",
        CategoriaProducao.ReunioesEquipe => "Reuniões de equipe",
        CategoriaProducao.ReunioesCoordenacao => "Reuniões com coordenação, diretoria ou gestão",
        CategoriaProducao.ElaboracaoPei => "Elaboração de PEI",
        CategoriaProducao.Administrativo => "Encaminhamentos, agenda, relatórios e tarefas administrativas",
        CategoriaProducao.AtividadesCursos => "Atividades nos cursos",
        _ => throw new ArgumentOutOfRangeException(nameof(c)),
    };
}
