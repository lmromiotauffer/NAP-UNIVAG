using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Configuracoes;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Painel;
using Nap.Application.Features.Peis;

namespace Nap.Application.Tests.Fakes;

public sealed class PainelQueriesFalso : IPainelQueries
{
    public SemanaBrutaDto Semana { get; set; } = new(0, 0, 0, 0, 0);
    public List<SerieMensalDto> Serie { get; set; } = [];
    public (DateOnly Inicio, DateOnly Fim, Guid? ProfissionalId, Guid? CursoId)? SemanaRecebida { get; private set; }
    public (DateOnly Mes1, DateOnly Mes2)? SerieRecebida { get; private set; }
    public Guid? ProfissionalDaAgenda { get; private set; }
    public Guid? ProfissionalDaSerie { get; private set; }
    public Guid? CursoDaSituacao { get; private set; }
    public Guid? ProfissionalDosAlertas { get; private set; }
    public ConfiguracaoAlertasDto? OpcoesRecebidas { get; private set; }
    public EncaminhamentosPainelDto Encaminhamentos { get; set; } = new(0, 0, 0, 0, 0);
    public List<AlertaDto> AlertasEncaminhamento { get; set; } = [];
    public (EscopoEncaminhamentos Escopo, Guid? SolicitanteId)? ContagemRecebida { get; private set; }
    public (EscopoEncaminhamentos Escopo, Guid? SolicitanteId, bool SemDestinatario)? AlertasEncaminhamentoRecebidos { get; private set; }
    public PeisPainelDto Peis { get; set; } = new(0, 0, 0);
    public List<AlertaDto> AlertasPei { get; set; } = [];
    public (EscopoPeis Escopo, Guid? ResponsavelId, Guid? CursoId)? PeisRecebidos { get; private set; }
    public (Guid? ResponsavelId, Guid? CursoId, bool SemCoordenador)? AlertasPeiRecebidos { get; private set; }
    public AtividadesPainelDto Atividades { get; set; } = new(0, 0, []);
    public (Guid? ProfissionalId, Guid? CursoId)? AtividadesRecebidas { get; private set; }
    public List<string> Chamadas { get; } = [];

    public Task<SemanaBrutaDto> ObterSemanaAsync(DateOnly inicio, DateOnly fim, Guid? profissionalId, Guid? cursoId, CancellationToken ct)
    {
        Chamadas.Add(nameof(ObterSemanaAsync));
        SemanaRecebida = (inicio, fim, profissionalId, cursoId);
        return Task.FromResult(Semana);
    }

    public Task<IReadOnlyList<CompromissoDto>> ObterAgendaDoDiaAsync(DateOnly dia, Guid? profissionalId, CancellationToken ct)
    {
        Chamadas.Add(nameof(ObterAgendaDoDiaAsync));
        ProfissionalDaAgenda = profissionalId;
        return Task.FromResult<IReadOnlyList<CompromissoDto>>([]);
    }

    public Task<IReadOnlyList<SerieMensalDto>> ObterSerieMensalAsync(DateOnly mesInicial, DateOnly mesFinal, Guid? profissionalId, Guid? cursoId, CancellationToken ct)
    {
        Chamadas.Add(nameof(ObterSerieMensalAsync));
        SerieRecebida = (mesInicial, mesFinal);
        ProfissionalDaSerie = profissionalId;
        return Task.FromResult<IReadOnlyList<SerieMensalDto>>(Serie);
    }

    public Task<IReadOnlyList<ContagemDto>> ContarAlunosPorCursoAsync(Guid? cursoId, CancellationToken ct)
    {
        Chamadas.Add(nameof(ContarAlunosPorCursoAsync));
        return Task.FromResult<IReadOnlyList<ContagemDto>>([new("c", "Curso", 2)]);
    }

    public Task<IReadOnlyList<ContagemDto>> ContarAlunosPorSituacaoAsync(Guid? cursoId, CancellationToken ct)
    {
        Chamadas.Add(nameof(ContarAlunosPorSituacaoAsync));
        CursoDaSituacao = cursoId;
        return Task.FromResult<IReadOnlyList<ContagemDto>>([new("ativo", "Ativo", 5)]);
    }

    public Task<IReadOnlyList<AlertaDto>> ObterAlertasAsync(DateOnly hoje, Guid? profissionalId, Guid? cursoId, ConfiguracaoAlertasDto opcoes, CancellationToken ct)
    {
        Chamadas.Add(nameof(ObterAlertasAsync));
        ProfissionalDosAlertas = profissionalId;
        OpcoesRecebidas = opcoes;
        return Task.FromResult<IReadOnlyList<AlertaDto>>([new("sem_desfecho", "Sem desfecho", "Detalhe", Guid.NewGuid(), 1)]);
    }

    public Task<IReadOnlyList<ProducaoProfissionalDto>> ObterPorProfissionalAsync(DateOnly inicio, DateOnly fim, Guid? cursoId, CancellationToken ct)
    {
        Chamadas.Add(nameof(ObterPorProfissionalAsync));
        return Task.FromResult<IReadOnlyList<ProducaoProfissionalDto>>([new(Guid.NewGuid(), "Prof", 1, 0, 50)]);
    }

    public Task<EncaminhamentosPainelDto> ContarEncaminhamentosAsync(EscopoEncaminhamentos e, Guid? solicitanteId, DateOnly hoje, ConfiguracaoAlertasDto opcoes, CancellationToken ct)
    {
        Chamadas.Add(nameof(ContarEncaminhamentosAsync));
        ContagemRecebida = (e, solicitanteId);
        return Task.FromResult(Encaminhamentos with { DiasSemAtualizacao = opcoes.DiasSemAtualizacao });
    }

    public Task<IReadOnlyList<AlertaDto>> ObterAlertasEncaminhamentoAsync(EscopoEncaminhamentos e, Guid? solicitanteId, bool incluirSemDestinatario,
        DateOnly hoje, ConfiguracaoAlertasDto opcoes, CancellationToken ct)
    {
        Chamadas.Add(nameof(ObterAlertasEncaminhamentoAsync));
        AlertasEncaminhamentoRecebidos = (e, solicitanteId, incluirSemDestinatario);
        return Task.FromResult<IReadOnlyList<AlertaDto>>(AlertasEncaminhamento);
    }

    public Task<PeisPainelDto> ContarPeisAsync(EscopoPeis e, Guid? responsavelId, Guid? cursoId, CancellationToken ct)
    {
        Chamadas.Add(nameof(ContarPeisAsync));
        PeisRecebidos = (e, responsavelId, cursoId);
        return Task.FromResult(Peis);
    }

    public Task<IReadOnlyList<AlertaDto>> ObterAlertasPeiAsync(Guid? responsavelId, Guid? cursoId, bool incluirSemCoordenador, DateOnly hoje,
        ConfiguracaoAlertasDto opcoes, CancellationToken ct)
    {
        Chamadas.Add(nameof(ObterAlertasPeiAsync));
        AlertasPeiRecebidos = (responsavelId, cursoId, incluirSemCoordenador);
        return Task.FromResult<IReadOnlyList<AlertaDto>>(AlertasPei);
    }

    public Task<AtividadesPainelDto> ObterAtividadesAsync(DateOnly hoje, Guid? profissionalId, Guid? cursoId, CancellationToken ct)
    {
        Chamadas.Add(nameof(ObterAtividadesAsync));
        AtividadesRecebidas = (profissionalId, cursoId);
        return Task.FromResult(Atividades);
    }
}
