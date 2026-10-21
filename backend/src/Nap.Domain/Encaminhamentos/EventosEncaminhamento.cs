using Nap.Domain.Comum;

namespace Nap.Domain.Encaminhamentos;

public sealed record EncaminhamentoCriado(Guid EventoId, DateTimeOffset OcorridoEm, Guid EncaminhamentoId, Guid AlunoId,
    Guid CursoId, Guid SolicitanteId, Especialidade Especialidade, DestinoEncaminhamento Destino) : IEventoDominio
{
    internal EncaminhamentoCriado(DateTimeOffset ocorridoEm, Guid encaminhamentoId, Guid alunoId, Guid cursoId,
        Guid solicitanteId, Especialidade especialidade, DestinoEncaminhamento destino)
        : this(Guid.CreateVersion7(), ocorridoEm, encaminhamentoId, alunoId, cursoId, solicitanteId, especialidade, destino) { }
}

public sealed record RetornoEncaminhamentoRegistrado(Guid EventoId, DateTimeOffset OcorridoEm, Guid EncaminhamentoId,
    Guid AlunoId, Guid SolicitanteId, Guid AutorId, EstadoEncaminhamento Estado, AcaoRetorno Acao) : IEventoDominio
{
    internal RetornoEncaminhamentoRegistrado(DateTimeOffset ocorridoEm, Guid encaminhamentoId, Guid alunoId,
        Guid solicitanteId, Guid autorId, EstadoEncaminhamento estado, AcaoRetorno acao)
        : this(Guid.CreateVersion7(), ocorridoEm, encaminhamentoId, alunoId, solicitanteId, autorId, estado, acao) { }
}
