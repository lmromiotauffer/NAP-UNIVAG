using Nap.Domain.Comum;

namespace Nap.Domain.Agenda;

public enum ResultadoEscala { Ok, ForaDaEscala }

public class EscalaProfissional : Entidade
{
    private readonly List<FaixaEscala> _faixas = [];

    public Guid ProfissionalId { get; private set; }
    public DateOnly VigenciaInicio { get; private set; }
    public DateOnly? VigenciaFim { get; private set; }

    public IReadOnlyList<FaixaEscala> Faixas => _faixas;

    private EscalaProfissional() { } // ORM

    public static EscalaProfissional Criar(Guid profissionalId, DateOnly inicio, DateOnly? fim, IReadOnlyList<FaixaEscala> faixas)
    {
        if (profissionalId == Guid.Empty)
            throw new DomainException("O profissional é obrigatório.");
        ExigirVigencia(inicio, fim);
        if (faixas is null || faixas.Count == 0)
            throw new DomainException("Informe ao menos uma faixa na escala.");
        if (faixas.GroupBy(f => f.Dia).Any(g => g.Count() > 1))
            throw new DomainException("A escala pode ter no máximo uma faixa por dia.");
        var escala = new EscalaProfissional { ProfissionalId = profissionalId, VigenciaInicio = inicio, VigenciaFim = fim };
        escala._faixas.AddRange(faixas);
        return escala;
    }

    public void AtualizarVigenciaFim(DateOnly? fim)
    {
        ExigirVigencia(VigenciaInicio, fim);
        VigenciaFim = fim;
    }

    public bool Vigente(DateOnly data) => data >= VigenciaInicio && (VigenciaFim is null || data <= VigenciaFim);

    /// <summary>A vigente na data: a de início mais recente (vigências não se sobrepõem; o desempate só evita falhar se houver
    /// duas). Mesma escolha de <c>EscalaRepository.ObterVigenteAsync</c>, que a faz no banco.</summary>
    public static EscalaProfissional? VigenteEm(IEnumerable<EscalaProfissional> escalas, DateOnly data) =>
        escalas.Where(e => e.Vigente(data)).MaxBy(e => e.VigenciaInicio);

    // Só compara períodos; o chamador filtra pelo mesmo profissional.
    public bool SobrepoeA(EscalaProfissional outra) =>
        (VigenciaFim is null || outra.VigenciaInicio <= VigenciaFim) &&
        (outra.VigenciaFim is null || VigenciaInicio <= outra.VigenciaFim);

    public FaixaEscala? FaixaDe(DayOfWeek dia) => _faixas.SingleOrDefault(f => f.Dia == dia);

    public static ResultadoEscala Avaliar(FaixaEscala? faixa, TimeOnly inicio) =>
        faixa is not null && inicio >= faixa.Inicio && inicio < faixa.Fim ? ResultadoEscala.Ok : ResultadoEscala.ForaDaEscala;

    private static void ExigirVigencia(DateOnly inicio, DateOnly? fim)
    {
        if (fim is not null && fim < inicio)
            throw new DomainException("O fim da vigência não pode ser anterior ao início.");
    }
}
