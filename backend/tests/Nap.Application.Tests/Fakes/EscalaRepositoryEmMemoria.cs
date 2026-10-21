using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Domain.Agenda;

namespace Nap.Application.Tests.Fakes;

public sealed class EscalaRepositoryEmMemoria : IEscalaRepository
{
    public List<EscalaProfissional> Escalas { get; } = [];

    public Task<EscalaProfissional?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Escalas.FirstOrDefault(e => e.Id == id));

    public Task<EscalaProfissional?> ObterVigenteAsync(Guid profissionalId, DateOnly data, CancellationToken ct) =>
        Task.FromResult(Escalas.FirstOrDefault(e => e.ProfissionalId == profissionalId && e.Vigente(data)));

    public Task<IReadOnlyList<EscalaProfissional>> ListarDoProfissionalAsync(Guid profissionalId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EscalaProfissional>>(Escalas.Where(e => e.ProfissionalId == profissionalId).ToList());

    public Task<IReadOnlyList<EscalaProfissionalDto>> ListarAsync(Guid? profissionalId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EscalaProfissionalDto>>(Escalas.Where(e => profissionalId == null || e.ProfissionalId == profissionalId)
            .Select(e => new EscalaProfissionalDto(e.Id, e.ProfissionalId, "Profissional", e.VigenciaInicio, e.VigenciaFim,
                e.Faixas.Select(f => new FaixaEscalaDto(f.Dia, f.Inicio, f.Fim, f.SemAtendimento)).ToList(), "v"))
            .ToList());

    public void Adicionar(EscalaProfissional e) => Escalas.Add(e);

    public List<AutorizacaoForaEscala> Autorizacoes { get; } = [];

    public Task<AutorizacaoForaEscala?> ObterAutorizacaoAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Autorizacoes.FirstOrDefault(a => a.Id == id));

    public Task<bool> ExisteAutorizacaoAtivaAsync(Guid profissionalId, DateTimeOffset inicio, CancellationToken ct) =>
        Task.FromResult(Autorizacoes.Any(a => a.ProfissionalId == profissionalId && a.RevogadaEm == null
            && a.Intervalo.Inicio <= inicio && inicio < a.Intervalo.Fim));

    public Task<PagedResult<AutorizacaoForaEscalaDto>> ListarAutorizacoesAsync(Guid? profissionalId, DateTimeOffset? de, DateTimeOffset? ate,
        int pagina, int tamanho, CancellationToken ct)
    {
        var q = Autorizacoes.Where(a => (profissionalId == null || a.ProfissionalId == profissionalId)
            && (de == null || a.Intervalo.Fim > de) && (ate == null || a.Intervalo.Inicio < ate)).OrderByDescending(a => a.Intervalo.Inicio).ToList();
        var itens = q.Skip((pagina - 1) * tamanho).Take(tamanho).Select(a => new AutorizacaoForaEscalaDto(a.Id, a.ProfissionalId, "Profissional",
            a.Intervalo.Inicio, a.Intervalo.Fim, a.Justificativa, a.AutorizadoPorId, "Coordenação", a.CriadaEm, a.RevogadaEm)).ToList();
        return Task.FromResult(new PagedResult<AutorizacaoForaEscalaDto>(itens, pagina, tamanho, q.Count));
    }

    public void AdicionarAutorizacao(AutorizacaoForaEscala a) => Autorizacoes.Add(a);
}
