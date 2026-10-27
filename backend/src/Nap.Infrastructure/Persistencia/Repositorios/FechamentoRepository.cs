using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Producao;
using Nap.Domain.Producao;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class FechamentoRepository(NapDbContext db) : IFechamentoRepository
{
    public Task<FechamentoSemanal?> ObterAsync(Guid profissionalId, DateOnly semana, CancellationToken ct) =>
        db.FechamentosSemanais.Include(f => f.Eventos)
            .SingleOrDefaultAsync(f => f.ProfissionalId == profissionalId && f.Semana == semana, ct);

    public void Adicionar(FechamentoSemanal f) => db.FechamentosSemanais.Add(f);

    public Task<bool> ExisteFechadaAsync(Guid? profissionalId, DateOnly primeiraSemana, DateOnly? ate, CancellationToken ct) =>
        db.FechamentosSemanais.AnyAsync(f => f.Fechada && (profissionalId == null || f.ProfissionalId == profissionalId)
            && f.Semana >= primeiraSemana && (ate == null || f.Semana <= ate), ct);

    public async Task BloquearAsync(Guid profissionalId, bool exclusivo, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("O bloqueio de fechamento só é válido dentro de EmTransacaoAsync.");
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = {1}, @LockOwner = 'Transaction', @LockTimeout = 10000; IF @r < 0 THROW 51000, 'Fechamento em andamento; tente novamente', 1;",
                [$"fechamento:prof:{profissionalId:N}", exclusivo ? "Exclusive" : "Shared"], ct);
        }
        catch (SqlException e) when (e.Number == 51000)
        {
            throw new ConflictException("Fechamento em andamento; tente novamente");
        }
    }
}
