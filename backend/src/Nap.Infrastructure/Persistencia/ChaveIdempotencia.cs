using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;

namespace Nap.Infrastructure.Persistencia;

public class ChaveIdempotencia
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UsuarioId { get; private set; }
    public string Chave { get; private set; } = null!;
    public string Rota { get; private set; } = null!;
    public Guid? RecursoId { get; private set; }
    public DateTimeOffset CriadaEm { get; private set; }

    private ChaveIdempotencia() { } // ORM
}

/// <summary>Opera direto no banco (SQL/ExecuteUpdate/ExecuteDelete) para não gravar junto as alterações
/// pendentes do contexto, como a auditoria acumulada para o próximo SalvarAsync.</summary>
public sealed class ChavesIdempotencia(NapDbContext db, IRelogio relogio) : IChavesIdempotencia
{
    private static readonly TimeSpan ReservaAbandonada = TimeSpan.FromMinutes(5);

    public async Task<ReservaIdempotencia> ReservarAsync(Guid usuarioId, string chave, string rota, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO [nap].[ChavesIdempotencia] (Id, UsuarioId, Chave, Rota, RecursoId, CriadaEm) VALUES ({Guid.CreateVersion7()}, {usuarioId}, {chave}, {rota}, NULL, {relogio.Agora.ToUniversalTime()})",
                ct);
            return new ReservaIdempotencia(ResultadoReserva.Reservada, null);
        }
        catch (SqlException e) when (e.Number is 2601 or 2627)
        {
            var existente = await db.ChavesIdempotencia.AsNoTracking()
                .Where(c => c.UsuarioId == usuarioId && c.Chave == chave)
                .Select(c => new { c.Rota, c.RecursoId, c.CriadaEm })
                .SingleOrDefaultAsync(ct);
            // Liberada entre o INSERT e a leitura: o cliente pode simplesmente repetir.
            if (existente is null) return new ReservaIdempotencia(ResultadoReserva.EmAndamento, null);
            if (existente.Rota != rota)
                throw new ConflictException("Esta chave de idempotência já foi usada em outra operação");
            if (existente.RecursoId is { } recurso)
                return new ReservaIdempotencia(ResultadoReserva.Concluida, recurso);
            // Reserva em andamento há mais de 5 min: o processo que a fez caiu sem concluir nem liberar. Retoma
            // renovando CriadaEm só se ninguém a retomou antes (compara o CriadaEm lido), para um único vencedor.
            // ponytail: um original só lento (>5 min, vivo) ainda pode concluir junto; lease com dono se isso ocorrer.
            var agora = relogio.Agora;
            if (existente.CriadaEm < agora - ReservaAbandonada
                && await db.ChavesIdempotencia
                    .Where(c => c.UsuarioId == usuarioId && c.Chave == chave && c.RecursoId == null && c.CriadaEm == existente.CriadaEm)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.CriadaEm, agora), ct) == 1)
                return new ReservaIdempotencia(ResultadoReserva.Reservada, null);
            return new ReservaIdempotencia(ResultadoReserva.EmAndamento, null);
        }
    }

    public async Task ConcluirAsync(Guid usuarioId, string chave, Guid recursoId, CancellationToken ct)
    {
        var linhas = await db.ChavesIdempotencia
            .Where(c => c.UsuarioId == usuarioId && c.Chave == chave)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.RecursoId, recursoId), ct);
        if (linhas == 0)
            throw new InvalidOperationException("Reserva de idempotência inexistente; a conclusão não foi registrada.");
    }

    // Só remove reserva em andamento (concluída continua devolvendo o recurso). Sem linhas afetadas não é erro:
    // liberar é limpeza após falha e a reserva pode nem ter sido feita.
    public Task LiberarAsync(Guid usuarioId, string chave, CancellationToken ct) =>
        db.ChavesIdempotencia.Where(c => c.UsuarioId == usuarioId && c.Chave == chave && c.RecursoId == null).ExecuteDeleteAsync(ct);
}
