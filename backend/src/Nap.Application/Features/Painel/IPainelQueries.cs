namespace Nap.Application.Features.Painel;

public interface IPainelQueries
{
    Task<SemanaBrutaDto> ObterSemanaAsync(DateOnly inicio, DateOnly fim, Guid? profissionalId, Guid? cursoId, CancellationToken ct);

    Task<IReadOnlyList<Agenda.CompromissoDto>> ObterAgendaDoDiaAsync(DateOnly dia, Guid? profissionalId, CancellationToken ct);

    /// <summary>Um item por mês com dados, de <paramref name="mesInicial"/> a <paramref name="mesFinal"/> (primeiros dias dos meses); <c>Parcial</c> vem falso.</summary>
    Task<IReadOnlyList<SerieMensalDto>> ObterSerieMensalAsync(DateOnly mesInicial, DateOnly mesFinal, Guid? profissionalId, Guid? cursoId, CancellationToken ct);

    Task<IReadOnlyList<ContagemDto>> ContarAlunosPorCursoAsync(Guid? cursoId, CancellationToken ct);

    Task<IReadOnlyList<ContagemDto>> ContarAlunosPorSituacaoAsync(Guid? cursoId, CancellationToken ct);

    Task<IReadOnlyList<AlertaDto>> ObterAlertasAsync(DateOnly hoje, Guid? profissionalId, Guid? cursoId, Configuracoes.ConfiguracaoAlertasDto opcoes, CancellationToken ct);

    /// <summary>Conta os encaminhamentos abertos no escopo; <paramref name="solicitanteId"/> restringe a quem os criou.</summary>
    Task<EncaminhamentosPainelDto> ContarEncaminhamentosAsync(Encaminhamentos.EscopoEncaminhamentos e, Guid? solicitanteId, DateOnly hoje, Configuracoes.ConfiguracaoAlertasDto opcoes, CancellationToken ct);

    /// <summary>Alertas de encaminhamento (sem acolhimento no prazo e, se pedido, sem destinatário); nunca trazem o motivo.</summary>
    Task<IReadOnlyList<AlertaDto>> ObterAlertasEncaminhamentoAsync(Encaminhamentos.EscopoEncaminhamentos e, Guid? solicitanteId,
        bool incluirSemDestinatario, DateOnly hoje, Configuracoes.ConfiguracaoAlertasDto opcoes, CancellationToken ct);

    /// <summary>Conta versões de PEI no escopo; <paramref name="responsavelId"/> restringe aos PEIs do profissional responsável e <paramref name="cursoId"/> aos alunos do curso atual.</summary>
    Task<Peis.PeisPainelDto> ContarPeisAsync(Peis.EscopoPeis e, Guid? responsavelId, Guid? cursoId, CancellationToken ct);

    /// <summary>Alertas <c>pei-pendente</c> e, se pedido, <c>pei-sem-coordenador</c>; nunca trazem a demanda interna.</summary>
    Task<IReadOnlyList<AlertaDto>> ObterAlertasPeiAsync(Guid? responsavelId, Guid? cursoId, bool incluirSemCoordenador, DateOnly hoje, Configuracoes.ConfiguracaoAlertasDto opcoes, CancellationToken ct);

    /// <summary>Atividades do mês civil de <paramref name="hoje"/>; <paramref name="profissionalId"/> e <paramref name="cursoId"/> restringem o escopo.</summary>
    Task<AtividadesPainelDto> ObterAtividadesAsync(DateOnly hoje, Guid? profissionalId, Guid? cursoId, CancellationToken ct);

    Task<IReadOnlyList<ProducaoProfissionalDto>> ObterPorProfissionalAsync(DateOnly inicio, DateOnly fim, Guid? cursoId, CancellationToken ct);
}
