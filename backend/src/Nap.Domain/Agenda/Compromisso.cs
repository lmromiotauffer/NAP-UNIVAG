using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Domain.Agenda;

public class Compromisso : Entidade
{
    private readonly List<CorrecaoRealizacao> _correcoesRealizacao = [];

    public TipoCompromisso Tipo { get; private set; }
    public EstadoCompromisso Estado { get; private set; }
    public string Titulo { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public IntervaloHorario Intervalo { get; private set; } = null!;
    public Guid ProfissionalId { get; private set; }
    public Guid? AlunoId { get; private set; }
    public Guid CriadoPorId { get; private set; }
    /// <summary>Agrupa as instâncias de um compromisso de equipe (uma por participante); definido só na criação.</summary>
    public Guid? GrupoId { get; private set; }
    public Guid? RemarcadoDeId { get; private set; }
    public Guid? RemarcadoParaId { get; private set; }
    public Guid? RegistroVinculadoId { get; private set; }
    public string? MotivoCancelamento { get; private set; }
    public DateTimeOffset? RealizacaoInicio { get; private set; }
    public DateTimeOffset? RealizacaoFim { get; private set; }
    public Guid? RealizadoPorId { get; private set; }
    public IReadOnlyCollection<CorrecaoRealizacao> CorrecoesRealizacao => _correcoesRealizacao;

    /// <summary>Horário efetivo da realização, quando registrada; senão o agendado.</summary>
    public IntervaloHorario IntervaloEfetivo =>
        RealizacaoInicio is { } inicio && RealizacaoFim is { } fim ? IntervaloHorario.Criar(inicio, fim) : Intervalo;

    private Compromisso() { } // ORM

    public static Compromisso Criar(TipoCompromisso tipo, string titulo, IntervaloHorario intervalo,
        Guid profissionalId, Guid? alunoId, string? descricao, Guid criadoPorId, Guid? grupoId = null)
    {
        ArgumentNullException.ThrowIfNull(intervalo);
        titulo = (titulo ?? string.Empty).Trim();
        if (titulo.Length is < 1 or > 120)
            throw new DomainException("O título deve ter de 1 a 120 caracteres.");
        if (tipo == TipoCompromisso.Atendimento && alunoId is null)
            throw new DomainException("Um atendimento na agenda exige um aluno.");
        if (tipo is TipoCompromisso.Bloqueio or TipoCompromisso.Lembrete && alunoId is not null)
            throw new DomainException("Bloqueios e lembretes não podem estar vinculados a um aluno.");

        var c = new Compromisso
        {
            Tipo = tipo,
            Estado = EstadoCompromisso.Agendado,
            Titulo = titulo,
            Descricao = descricao,
            Intervalo = intervalo,
            ProfissionalId = profissionalId,
            AlunoId = alunoId,
            CriadoPorId = criadoPorId,
            GrupoId = grupoId,
        };
        return c;
    }

    public void Atualizar(string titulo, string? descricao)
    {
        ExigirAgendado("alterado");
        titulo = (titulo ?? string.Empty).Trim();
        if (titulo.Length is < 1 or > 120)
            throw new DomainException("O título deve ter de 1 a 120 caracteres.");
        Titulo = titulo;
        Descricao = descricao;
    }

    public Compromisso Remarcar(IntervaloHorario novo, Guid autorId)
    {
        ExigirAgendado("remarcado");
        var remarcado = Criar(Tipo, Titulo, novo, ProfissionalId, AlunoId, Descricao, autorId, GrupoId);
        remarcado.RemarcadoDeId = Id;
        Estado = EstadoCompromisso.Remarcado;
        RemarcadoParaId = remarcado.Id;
        return remarcado;
    }

    /// <summary>Autor e instante do cancelamento ficam na auditoria.</summary>
    public void Cancelar(string motivo)
    {
        ExigirAgendado("cancelado");
        motivo = (motivo ?? string.Empty).Trim();
        if (motivo.Length is < 3 or > 500)
            throw new DomainException("O motivo do cancelamento é obrigatório e deve ter de 3 a 500 caracteres.");
        Estado = EstadoCompromisso.Cancelado;
        MotivoCancelamento = motivo;
    }

    public void RegistrarRealizacao(IntervaloHorario efetivo, Guid autorId, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(efetivo);
        ExigirAgendado("realização registrada");
        if (!Tipo.ExigeRealizacao())
            throw new DomainException("Só reuniões, elaboração de PEI e tarefas administrativas têm a realização registrada pela agenda.");
        if (Intervalo.Fim > agora || efetivo.Fim > agora)
            throw new DomainException("Só é possível registrar a realização depois do término.");
        Estado = EstadoCompromisso.Concluido;
        RealizacaoInicio = efetivo.Inicio;
        RealizacaoFim = efetivo.Fim;
        RealizadoPorId = autorId;
    }

    public void CorrigirRealizacao(IntervaloHorario efetivo, string justificativa, Guid autorId, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(efetivo);
        if (Estado != EstadoCompromisso.Concluido || RealizacaoInicio is not { } inicioAnterior || RealizacaoFim is not { } fimAnterior)
            throw new DomainException("Só é possível corrigir a realização de um compromisso com realização registrada.");
        if (efetivo.Fim > agora)
            throw new DomainException("Só é possível registrar a realização depois do término.");
        justificativa = (justificativa ?? string.Empty).Trim();
        if (justificativa.Length is < 5 or > 500)
            throw new DomainException("A justificativa da correção é obrigatória e deve ter de 5 a 500 caracteres.");
        _correcoesRealizacao.Add(CorrecaoRealizacao.Criar(autorId, agora, justificativa, inicioAnterior, fimAnterior));
        RealizacaoInicio = efetivo.Inicio;
        RealizacaoFim = efetivo.Fim;
    }

    public void Concluir(Guid registroId)
    {
        ExigirAgendado("concluído");
        Estado = EstadoCompromisso.Concluido;
        RegistroVinculadoId = registroId;
    }

    private void ExigirAgendado(string acao)
    {
        if (Estado != EstadoCompromisso.Agendado)
            throw new DomainException($"Só é possível alterar o compromisso quando ele está Agendado (ação: {acao}).");
    }
}
