using Nap.Application.Comum;
using Nap.Domain.Agenda;

namespace Nap.Application.Features.Agenda;

/// <summary>Alcance de remarcar, atualizar e cancelar: só a instância endereçada ou todas as instâncias agendadas do grupo.</summary>
public enum EscopoGrupo
{
    Minha,
    Grupo,
}

public static class GrupoCompromisso
{
    public const int MaximoParticipantes = 50;
    public const string MensagemMinhaEmGrupo = "Num compromisso de grupo, só é possível cancelar a sua instância; para remarcar ou atualizar, use o escopo do grupo (criador ou coordenação).";

    /// <summary>Ordem das chaves <c>agenda:prof:{id:N}</c>/<c>fechamento:prof:{id:N}</c>: todas as travas de um tipo são tomadas nela, sem intercalar.</summary>
    public static IReadOnlyList<Guid> EmOrdemDeTrava(IEnumerable<Guid> profissionaisIds) =>
        [.. profissionaisIds.Distinct().OrderBy(id => id.ToString("N"), StringComparer.Ordinal)];

    /// <summary>
    /// Instâncias a alterar, a endereçada primeiro (o domínio barra se ela não estiver Agendada). <c>Minha</c>: a regra de sempre
    /// (responsável ou coordenação); em instância de grupo só vale para cancelar (<c>minhaEmGrupo</c>), senão 400 em escopo. <c>Grupo</c>: criador do grupo ou coordenação, sobre as instâncias ainda Agendadas. As irmãs são lidas
    /// antes das travas: alteração concorrente nelas cai no rowversion ao salvar (a transação inteira é desfeita).
    /// </summary>
    public static async Task<IReadOnlyList<Compromisso>> InstanciasDoEscopoAsync(this IAgendaRepository agenda, Compromisso compromisso,
        EscopoGrupo escopo, IUsuarioAtual usuario, CancellationToken ct, bool minhaEmGrupo = false)
    {
        if (escopo == EscopoGrupo.Minha)
        {
            if (!usuario.EhCoordenacao() && compromisso.ProfissionalId != usuario.Id) throw new ForbiddenException();
            if (!minhaEmGrupo && compromisso.GrupoId is not null)
                throw Validacao.ErroDeCampo("escopo", MensagemMinhaEmGrupo);
            return [compromisso];
        }
        if (compromisso.GrupoId is not { } grupoId)
            throw Validacao.ErroDeCampo("escopo", "O compromisso não é de grupo.");

        var grupo = await agenda.ListarDoGrupoAsync(grupoId, ct);
        // Remarcar grava o autor da remarcação em CriadoPorId; o criador do grupo é o das instâncias originais.
        var criador = grupo.First(c => c.RemarcadoDeId is null).CriadoPorId;
        if (!PodeGerirGrupo(usuario, criador)) throw new ForbiddenException();
        return [compromisso, .. grupo.Where(c => c.Id != compromisso.Id && c.Estado == EstadoCompromisso.Agendado)];
    }

    /// <summary>Quem edita o grupo todo: coordenação ou o criador (o das instâncias originais, <c>RemarcadoDeId is null</c>).</summary>
    public static bool PodeGerirGrupo(IUsuarioAtual usuario, Guid criadorOriginalId) =>
        usuario.EhCoordenacao() || criadorOriginalId == usuario.Id;
}
