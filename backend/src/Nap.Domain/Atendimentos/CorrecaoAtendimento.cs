using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Domain.Atendimentos;

public class CorrecaoAtendimento : Entidade
{
    public Guid AutorId { get; private set; }
    public DateTimeOffset Em { get; private set; }
    public string? Justificativa { get; private set; }
    public EstadoAtendimento EstadoAnterior { get; private set; }
    public EstadoAtendimento EstadoNovo { get; private set; }
    public TipoAtendimento TipoAnterior { get; private set; }
    public DateTimeOffset InicioAnterior { get; private set; }
    public DateTimeOffset FimAnterior { get; private set; }
    public DadosClinicos? DadosAnteriores { get; private set; }

    private CorrecaoAtendimento() { } // ORM

    internal static CorrecaoAtendimento Criar(Guid autorId, DateTimeOffset em, string? justificativa,
        EstadoAtendimento anterior, EstadoAtendimento novo, TipoAtendimento tipoAnterior,
        IntervaloHorario intervaloAnterior, DadosClinicos? dadosAnteriores) =>
        new()
        {
            AutorId = autorId, Em = em, Justificativa = justificativa, EstadoAnterior = anterior, EstadoNovo = novo,
            TipoAnterior = tipoAnterior, InicioAnterior = intervaloAnterior.Inicio, FimAnterior = intervaloAnterior.Fim,
            DadosAnteriores = dadosAnteriores,
        };
}
