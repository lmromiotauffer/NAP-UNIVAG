using Nap.Application.Comum;
using Nap.Domain.Agenda;

namespace Nap.Application.Features.Agenda;

public record EscalaProfissionalDto(Guid Id, Guid ProfissionalId, string Profissional, DateOnly VigenciaInicio,
    DateOnly? VigenciaFim, IReadOnlyList<FaixaEscalaDto> Faixas, string Versao);

public record AutorizacaoForaEscalaDto(Guid Id, Guid ProfissionalId, string Profissional, DateTimeOffset Inicio, DateTimeOffset Fim,
    string Justificativa, Guid AutorizadoPorId, string AutorizadoPor, DateTimeOffset CriadaEm, DateTimeOffset? RevogadaEm);

public interface IEscalaRepository
{
    Task<EscalaProfissional?> ObterAsync(Guid id, CancellationToken ct);
    /// <summary>A escala cuja vigência contém a data; null quando vale a padrão.</summary>
    Task<EscalaProfissional?> ObterVigenteAsync(Guid profissionalId, DateOnly data, CancellationToken ct);
    Task<IReadOnlyList<EscalaProfissional>> ListarDoProfissionalAsync(Guid profissionalId, CancellationToken ct);
    /// <summary>Por profissional e início de vigência (mais recente primeiro).</summary>
    Task<IReadOnlyList<EscalaProfissionalDto>> ListarAsync(Guid? profissionalId, CancellationToken ct);
    void Adicionar(EscalaProfissional e);

    Task<AutorizacaoForaEscala?> ObterAutorizacaoAsync(Guid id, CancellationToken ct);
    /// <summary>Há autorização ativa (não revogada) do profissional que cobre o instante de início.</summary>
    Task<bool> ExisteAutorizacaoAtivaAsync(Guid profissionalId, DateTimeOffset inicio, CancellationToken ct);
    /// <summary>Por início do intervalo (mais recente primeiro). <c>De</c>/<c>Ate</c>: autorizações cujo intervalo toca [De, Ate).</summary>
    Task<PagedResult<AutorizacaoForaEscalaDto>> ListarAutorizacoesAsync(Guid? profissionalId, DateTimeOffset? de, DateTimeOffset? ate,
        int pagina, int tamanho, CancellationToken ct);
    void AdicionarAutorizacao(AutorizacaoForaEscala a);
}
