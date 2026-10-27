using Nap.Domain.Agenda;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Domain.Atividades;

public class AtividadeCurso : Entidade
{
    private readonly List<CorrecaoAtividade> _correcoes = [];

    public Guid ProfissionalId { get; private set; }
    public Guid? CursoId { get; private set; }
    public Guid? CompromissoId { get; private set; }
    public IntervaloHorario Intervalo { get; private set; } = null!;
    public TipoAtividade Tipo { get; private set; }
    public string Descricao { get; private set; } = null!;
    public string? JustificativaAvulso { get; private set; }
    public Guid CriadoPorId { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    public IReadOnlyCollection<CorrecaoAtividade> Correcoes => _correcoes;

    private AtividadeCurso() { } // ORM

    public static AtividadeCurso RegistrarDeCompromisso(Compromisso c, IntervaloHorario intervalo, Guid? cursoId,
        TipoAtividade tipo, string descricao, Guid autorId, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Tipo != TipoCompromisso.AtividadeCurso || c.Estado != EstadoCompromisso.Agendado)
            throw new DomainException("Só é possível registrar atividade a partir de um compromisso de atividade no curso agendado.");

        var a = Criar(c.ProfissionalId, c.Id, intervalo, cursoId, tipo, descricao, null, autorId, agora);
        c.Concluir(a.Id);
        return a;
    }

    public static AtividadeCurso RegistrarAvulsa(Guid profissionalId, IntervaloHorario intervalo, Guid? cursoId,
        TipoAtividade tipo, string descricao, string justificativaAvulso, Guid autorId, DateTimeOffset agora)
    {
        justificativaAvulso = (justificativaAvulso ?? string.Empty).Trim();
        if (justificativaAvulso.Length is < 5 or > 500)
            throw new DomainException("A justificativa da atividade avulsa é obrigatória e deve ter de 5 a 500 caracteres.");
        return Criar(profissionalId, null, intervalo, cursoId, tipo, descricao, justificativaAvulso, autorId, agora);
    }

    public void Corrigir(IntervaloHorario intervalo, Guid? cursoId, TipoAtividade tipo, string descricao,
        string justificativa, Guid autorId, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(intervalo);
        justificativa = (justificativa ?? string.Empty).Trim();
        if (justificativa.Length is < 5 or > 500)
            throw new DomainException("A justificativa da correção é obrigatória e deve ter de 5 a 500 caracteres.");
        descricao = Texto.Validar(descricao, 3, 2000, "A descrição da atividade");
        ValidarTermino(intervalo, agora);

        _correcoes.Add(CorrecaoAtividade.Criar(autorId, agora, justificativa, Intervalo, CursoId, Tipo, Descricao));
        Intervalo = intervalo;
        CursoId = cursoId;
        Tipo = tipo;
        Descricao = descricao;
    }

    // Recebe o início no fuso institucional; a conversão fica na Application.
    public static Turno TurnoDe(TimeOnly inicioLocal) =>
        inicioLocal.Hour < 12 ? Turno.Manha : inicioLocal.Hour < 18 ? Turno.Tarde : Turno.Noite;

    private static AtividadeCurso Criar(Guid profissionalId, Guid? compromissoId, IntervaloHorario intervalo,
        Guid? cursoId, TipoAtividade tipo, string descricao, string? justificativaAvulso, Guid autorId, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(intervalo);
        descricao = Texto.Validar(descricao, 3, 2000, "A descrição da atividade");
        ValidarTermino(intervalo, agora);

        return new AtividadeCurso
        {
            ProfissionalId = profissionalId,
            CursoId = cursoId,
            CompromissoId = compromissoId,
            Intervalo = intervalo,
            Tipo = tipo,
            Descricao = descricao,
            JustificativaAvulso = justificativaAvulso,
            CriadoPorId = autorId,
            CriadoEm = agora,
        };
    }

    private static void ValidarTermino(IntervaloHorario intervalo, DateTimeOffset agora)
    {
        if (intervalo.Fim > agora)
            throw new DomainException("Não é possível registrar atividade que ainda não terminou.");
    }
}
