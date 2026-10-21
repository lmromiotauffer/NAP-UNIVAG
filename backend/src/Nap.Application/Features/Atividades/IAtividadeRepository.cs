using Nap.Domain.Atividades;

namespace Nap.Application.Features.Atividades;

public interface IAtividadeRepository
{
    Task<AtividadeCurso?> ObterAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteParaCompromissoAsync(Guid compromissoId, CancellationToken ct);
    void Adicionar(AtividadeCurso a);
}
