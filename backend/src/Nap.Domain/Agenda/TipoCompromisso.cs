using Nap.Domain.Producao;

namespace Nap.Domain.Agenda;

public enum TipoCompromisso
{
    Atendimento,
    ReuniaoEquipe,
    ReuniaoCoordenacaoGestao,
    ElaboracaoPei,
    TarefaAdministrativa,
    EncaminhamentoDevolutiva,
    AtividadeCurso,
    Bloqueio,
    Lembrete,
}

public static class TipoCompromissoExtensoes
{
    public static bool OcupaHorario(this TipoCompromisso t) => t != TipoCompromisso.Lembrete;

    /// <summary>Lembrete e bloqueio podem cair fora da escala e em feriado.</summary>
    public static bool SegueEscala(this TipoCompromisso t) => t is not (TipoCompromisso.Lembrete or TipoCompromisso.Bloqueio);

    /// <summary>Tipos que aceitam participantes (compromisso de grupo).</summary>
    public static bool PermiteGrupo(this TipoCompromisso t) => t is TipoCompromisso.ReuniaoEquipe
        or TipoCompromisso.ReuniaoCoordenacaoGestao or TipoCompromisso.Lembrete;

    /// <summary>Tipos sem registro próprio: só viram produção por "Registrar realização".</summary>
    public static bool ExigeRealizacao(this TipoCompromisso t) => t is TipoCompromisso.ReuniaoEquipe
        or TipoCompromisso.ReuniaoCoordenacaoGestao or TipoCompromisso.ElaboracaoPei
        or TipoCompromisso.TarefaAdministrativa or TipoCompromisso.EncaminhamentoDevolutiva;

    /// <summary>Categoria dos tipos que exigem realização. Atendimento e atividade ficam de fora: contam pelos próprios registros.</summary>
    public static CategoriaProducao? CategoriaDe(this TipoCompromisso t) => t switch
    {
        TipoCompromisso.ReuniaoEquipe => CategoriaProducao.ReunioesEquipe,
        TipoCompromisso.ReuniaoCoordenacaoGestao => CategoriaProducao.ReunioesCoordenacao,
        TipoCompromisso.ElaboracaoPei => CategoriaProducao.ElaboracaoPei,
        TipoCompromisso.TarefaAdministrativa or TipoCompromisso.EncaminhamentoDevolutiva => CategoriaProducao.Administrativo,
        _ => null,
    };
}
