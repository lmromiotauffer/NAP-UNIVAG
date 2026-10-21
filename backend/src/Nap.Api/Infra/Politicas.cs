using Microsoft.AspNetCore.Authorization;
using Nap.Domain.Comum;

namespace Nap.Api.Infra;

/// <summary>Políticas nomeadas (ação). O recorte por recurso (escopo) fica nas Queries/handlers.
/// Nomes com o mesmo conjunto de perfis são apelidos da política canônica (uma única política registrada).</summary>
public static class Politicas
{
    public const string PodeConsultarAlunos = nameof(PodeConsultarAlunos);
    /// <summary>Encaminhamentos: Equipe NAP, Sip e CoordenadorCurso. O recorte por recurso e por campo fica nas Queries/handlers.</summary>
    public const string PodeConsultarEncaminhamentos = nameof(PodeConsultarEncaminhamentos);
    /// <summary>Comunicações: todos os perfis exceto Gestão (403). Só participantes veem a mensagem; o recorte fica nos handlers.</summary>
    public const string PodeUsarComunicacoes = PodeConsultarEncaminhamentos;
    /// <summary>PEI: Equipe NAP e CoordenadorCurso (só versões publicadas do próprio curso; o recorte fica nos handlers).</summary>
    public const string PodeConsultarPeis = PodeConsultarAlunos;
    /// <summary>Aprovar e devolver a revisão, publicar e arquivar o PEI: Coordenação (Administrador e CoordenadorNap).</summary>
    public const string PodePublicarPei = PodeAdministrar;
    /// <summary>Administração (usuários, cursos, escalas, feriados e limites de alerta): Administrador e CoordenadorNap. O que o CoordenadorNap não pode (administradores) é decidido nos handlers.</summary>
    public const string PodeAdministrar = nameof(PodeAdministrar);
    /// <summary>Gerenciar alunos, agenda e atendimentos. Correção própria × justificada é decidida pelo handler.</summary>
    public const string EquipeNap = nameof(EquipeNap);
    /// <summary>Registrar e corrigir atividades nos cursos: Equipe NAP. Própria × coordenação é decidido no handler.</summary>
    public const string PodeRegistrarAtividades = EquipeNap;
    /// <summary>Consultar atividades: Equipe NAP e CoordenadorCurso (só o próprio curso; o recorte fica nos handlers).</summary>
    public const string PodeConsultarAtividades = PodeConsultarAlunos;
    /// <summary>Consultar a produção: Equipe NAP, Gestão e CoordenadorCurso (recorte por perfil nos handlers).</summary>
    public const string PodeConsultarProducao = nameof(PodeConsultarProducao);
    /// <summary>Reabrir semanas e lançar ajustes de produção: Administrador e CoordenadorNap.</summary>
    public const string PodeGerirProducao = PodeAdministrar;

    public static void Configurar(AuthorizationOptions o)
    {
        o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        string[] equipeNap = [.. Perfis.EquipeNap];
        o.AddPolicy(PodeConsultarAlunos, p => p.RequireRole([.. equipeNap, Perfis.CoordenadorCurso]));
        o.AddPolicy(PodeConsultarEncaminhamentos, p => p.RequireRole([.. equipeNap, Perfis.Sip, Perfis.CoordenadorCurso]));
        o.AddPolicy(PodeAdministrar, p => p.RequireRole(Perfis.Administrador, Perfis.CoordenadorNap));
        o.AddPolicy(EquipeNap, p => p.RequireRole(equipeNap));
        o.AddPolicy(PodeConsultarProducao, p => p.RequireRole([.. equipeNap, Perfis.Gestao, Perfis.CoordenadorCurso]));
    }
}
