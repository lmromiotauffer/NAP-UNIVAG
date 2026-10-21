using Nap.Domain.Agenda;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Domain.Atendimentos;

public sealed record DadosClinicos(
    string? DemandaPrincipal,
    string? ObservacoesPrivadas,
    string? RecomendacoesEncaminhamento,
    string? ProximosPassos);

public class Atendimento : Entidade
{
    private readonly List<CorrecaoAtendimento> _correcoes = [];

    public Guid AlunoId { get; private set; }
    public Guid ProfissionalId { get; private set; }
    public Guid? CompromissoId { get; private set; }
    public IntervaloHorario Intervalo { get; private set; } = null!;
    public TipoAtendimento Tipo { get; private set; }
    public EstadoAtendimento Estado { get; private set; }
    public DadosClinicos? Dados { get; private set; }
    public string? JustificativaAvulso { get; private set; }
    public Guid CriadoPorId { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    public IReadOnlyCollection<CorrecaoAtendimento> Correcoes => _correcoes;

    private Atendimento() { } // ORM

    public static Atendimento RegistrarDeCompromisso(Compromisso c, TipoAtendimento tipo, EstadoAtendimento estado,
        DadosClinicos? dados, Guid autorId, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Tipo != TipoCompromisso.Atendimento || c.Estado != EstadoCompromisso.Agendado || c.AlunoId is not { } alunoId)
            throw new DomainException("Só é possível registrar atendimento a partir de um compromisso de atendimento agendado.");

        var a = Criar(alunoId, c.ProfissionalId, c.Id, c.Intervalo, tipo, estado, dados, null, autorId, agora);
        c.Concluir(a.Id);
        return a;
    }

    public static Atendimento RegistrarAvulso(Guid alunoId, Guid profissionalId, IntervaloHorario intervalo,
        TipoAtendimento tipo, EstadoAtendimento estado, DadosClinicos? dados, string justificativaAvulso,
        Guid autorId, DateTimeOffset agora)
    {
        justificativaAvulso = (justificativaAvulso ?? string.Empty).Trim();
        if (justificativaAvulso.Length is < 5 or > 500)
            throw new DomainException("A justificativa do atendimento avulso é obrigatória e deve ter de 5 a 500 caracteres.");
        return Criar(alunoId, profissionalId, null, intervalo, tipo, estado, dados, justificativaAvulso, autorId, agora);
    }

    public void Corrigir(EstadoAtendimento estado, TipoAtendimento tipo, IntervaloHorario intervalo,
        DadosClinicos? dados, string? justificativa, Guid autorId, bool autorEhResponsavel, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(intervalo);
        justificativa = string.IsNullOrWhiteSpace(justificativa) ? null : justificativa.Trim();
        if (!autorEhResponsavel && justificativa is null)
            throw new DomainException("A correção feita por quem não é o profissional responsável exige justificativa.");
        if (justificativa is { Length: < 5 or > 500 })
            throw new DomainException("A justificativa da correção deve ter de 5 a 500 caracteres.");
        if (intervalo.Fim > agora)
            throw new DomainException("Não é possível registrar atendimento que ainda não terminou");
        ValidarCoerencia(estado, dados);

        _correcoes.Add(CorrecaoAtendimento.Criar(autorId, agora, justificativa, Estado, estado, Tipo, Intervalo, Dados));
        Estado = estado;
        Tipo = tipo;
        Intervalo = intervalo;
        Dados = dados;
    }

    private static Atendimento Criar(Guid alunoId, Guid profissionalId, Guid? compromissoId, IntervaloHorario intervalo,
        TipoAtendimento tipo, EstadoAtendimento estado, DadosClinicos? dados, string? justificativaAvulso,
        Guid autorId, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(intervalo);
        if (estado is not (EstadoAtendimento.Realizado or EstadoAtendimento.Ausente))
            throw new DomainException("O registro de um atendimento só pode iniciar como Realizado ou Ausente.");
        if (intervalo.Fim > agora)
            throw new DomainException("Não é possível registrar atendimento que ainda não terminou");
        ValidarCoerencia(estado, dados);

        var a = new Atendimento
        {
            AlunoId = alunoId,
            ProfissionalId = profissionalId,
            CompromissoId = compromissoId,
            Intervalo = intervalo,
            Tipo = tipo,
            Estado = estado,
            Dados = dados,
            JustificativaAvulso = justificativaAvulso,
            CriadoPorId = autorId,
            CriadoEm = agora,
        };
        return a;
    }

    // Só Realizado exige demanda; nos demais estados o que vier é armazenado como está.
    private static void ValidarCoerencia(EstadoAtendimento estado, DadosClinicos? dados)
    {
        if (estado == EstadoAtendimento.Realizado && string.IsNullOrWhiteSpace(dados?.DemandaPrincipal))
            throw new DomainException("Um atendimento realizado exige a demanda principal.");
    }
}
