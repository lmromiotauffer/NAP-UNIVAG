using Nap.Domain.Comum;

namespace Nap.Domain.Encaminhamentos;

public class EvolucaoEncaminhamento : Entidade
{
    public DateTimeOffset Em { get; private set; }
    public Guid AutorId { get; private set; }
    public AcaoRetorno Acao { get; private set; }
    public EstadoEncaminhamento EstadoAnterior { get; private set; }
    public EstadoEncaminhamento EstadoNovo { get; private set; }
    public DateOnly? Data { get; private set; }
    public string? Texto { get; private set; }

    private EvolucaoEncaminhamento() { } // ORM

    internal static EvolucaoEncaminhamento Criar(DateTimeOffset em, Guid autorId, AcaoRetorno acao,
        EstadoEncaminhamento anterior, EstadoEncaminhamento novo, DateOnly? data, string? texto) =>
        new() { Em = em, AutorId = autorId, Acao = acao, EstadoAnterior = anterior, EstadoNovo = novo, Data = data, Texto = texto };
}
