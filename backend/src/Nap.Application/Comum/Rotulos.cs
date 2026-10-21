using Nap.Domain.Alunos;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comunicacoes;
using Nap.Domain.Encaminhamentos;
using Nap.Domain.Peis;

namespace Nap.Application.Comum;

/// <summary>Textos de exibição dos enums usados em títulos e rótulos montados nas consultas e nas notificações.</summary>
public static class Rotulos
{
    public static string De(SituacaoAluno s) => s switch
    {
        SituacaoAluno.EmAcompanhamento => "Em acompanhamento",
        _ => s.ToString(),
    };

    public static string De(TipoAtendimento t) => t switch
    {
        TipoAtendimento.AcolhimentoInicial => "Acolhimento inicial",
        TipoAtendimento.Psicopedagogico => "Psicopedagógico",
        TipoAtendimento.Reuniao => "Reunião",
        TipoAtendimento.OrientacaoAcademica => "Orientação acadêmica",
        _ => t.ToString(),
    };

    public static string De(Especialidade e) => e switch
    {
        Especialidade.PsicologiaEPsiquiatria => "Psicologia e Psiquiatria",
        Especialidade.CoordenacaoCurso => "Coordenação do curso",
        _ => e.ToString(),
    };

    public static string De(EstadoVersaoPei e) => e switch
    {
        EstadoVersaoPei.EmRevisao => "Em revisão",
        EstadoVersaoPei.AguardandoAssinaturas => "Aguardando assinaturas",
        _ => e.ToString(),
    };

    public static string De(DestinoEncaminhamento d) => d switch
    {
        DestinoEncaminhamento.Sip => "SIP",
        DestinoEncaminhamento.ClinicaEscola => "Clínica Escola",
        DestinoEncaminhamento.Externo => "Serviço externo",
        DestinoEncaminhamento.CoordenacaoCurso => "Coordenação do curso",
        _ => d.ToString(),
    };

    public static string De(AcaoRetorno a) => a switch
    {
        AcaoRetorno.InicioAtendimento => "Início do atendimento",
        AcaoRetorno.Evolucao => "Evolução",
        AcaoRetorno.Desistencia => "Desistência",
        _ => a.ToString(),
    };

    public static string De(EstadoEncaminhamento e) => e switch
    {
        EstadoEncaminhamento.EmAtendimento => "Em atendimento",
        EstadoEncaminhamento.Desistencia => "Desistência",
        _ => e.ToString(),
    };

    public static string De(TipoMensagem t) => t switch
    {
        TipoMensagem.EnvioPei => "Envio de PEI",
        TipoMensagem.SolicitacaoInformacao => "Solicitação de informação",
        TipoMensagem.RetornoEncaminhamento => "Retorno de encaminhamento",
        _ => t.ToString(),
    };
}
