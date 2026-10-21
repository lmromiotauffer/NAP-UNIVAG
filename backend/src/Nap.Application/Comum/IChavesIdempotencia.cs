namespace Nap.Application.Comum;

public enum ResultadoReserva { Reservada, Concluida, EmAndamento }

/// <summary>Resultado da reserva; <c>RecursoId</c> só vem preenchido quando a operação original já foi concluída.</summary>
public record ReservaIdempotencia(ResultadoReserva Resultado, Guid? RecursoId);

/// <summary>Chaves do header <c>Idempotency-Key</c>, únicas por usuário.</summary>
public interface IChavesIdempotencia
{
    /// <summary>Reserva a chave; se já existir, informa se a operação original terminou (e o recurso criado) ou ainda está em andamento.</summary>
    Task<ReservaIdempotencia> ReservarAsync(Guid usuarioId, string chave, string rota, CancellationToken ct);

    /// <summary>Marca a reserva como concluída com o recurso criado.</summary>
    Task ConcluirAsync(Guid usuarioId, string chave, Guid recursoId, CancellationToken ct);

    /// <summary>Remove a reserva quando a operação falhou, permitindo nova tentativa com a mesma chave.</summary>
    Task LiberarAsync(Guid usuarioId, string chave, CancellationToken ct);
}
