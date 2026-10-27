using Nap.Domain.Producao;

namespace Nap.Application.Features.Producao;

public interface IFechamentoRepository
{
    /// <summary>Fechamento da semana (identificada pela segunda-feira) do profissional, com os eventos; nulo se nunca foi fechada.</summary>
    Task<FechamentoSemanal?> ObterAsync(Guid profissionalId, DateOnly semana, CancellationToken ct);
    void Adicionar(FechamentoSemanal f);

    /// <summary>Alguma semana fechada (segunda entre <paramref name="primeiraSemana"/> e <paramref name="ate"/>, ou sem fim);
    /// <paramref name="profissionalId"/> nulo = qualquer profissional.</summary>
    Task<bool> ExisteFechadaAsync(Guid? profissionalId, DateOnly primeiraSemana, DateOnly? ate, CancellationToken ct);

    /// <summary>
    /// Trava de aplicação por profissional (<c>fechamento:prof:{id}</c>), presa à transação: compartilhada para quem escreve na semana,
    /// exclusiva para quem fecha/reabre. Só é válida dentro de EmTransacaoAsync e deve ser tomada antes das travas de agenda.
    /// </summary>
    Task BloquearAsync(Guid profissionalId, bool exclusivo, CancellationToken ct);
}
