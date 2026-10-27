using Nap.Domain.Producao;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Tests.Fakes;

public sealed class ProducaoQueriesFalso : IProducaoQueries
{
    public int Pendentes { get; set; }
    public IReadOnlyList<ProfissionalDto>? ProfissionaisRecebidos { get; private set; }
    public FatosProducao Fatos { get; set; } = new([], [], [], [], [], []);
    public IReadOnlyList<Guid>? IdsRecebidos { get; private set; }
    public Guid? CursoRecebido { get; private set; }
    public (DateOnly Inicio, DateOnly Fim)? JanelaRecebida { get; private set; }

    public Task<int> ContarPendentesAsync(Guid profissionalId, DateOnly semana, CancellationToken ct) => Task.FromResult(Pendentes);

    public int EmAndamento { get; set; }

    public Task<int> ContarEmAndamentoAsync(Guid profissionalId, DateOnly semana, CancellationToken ct) => Task.FromResult(EmAndamento);

    public Task<IReadOnlyList<SemanaProducaoDto>> ListarSemanasAsync(IReadOnlyList<ProfissionalDto> profissionais, DateOnly inicio,
        DateOnly fim, CancellationToken ct)
    {
        ProfissionaisRecebidos = profissionais;
        return Task.FromResult<IReadOnlyList<SemanaProducaoDto>>(profissionais
            .Select(p => new SemanaProducaoDto(p.Id, p.Nome, inicio, false, Pendentes, null, [])).ToList());
    }

    /// <summary>Recorta por profissional como o SQL faria; o curso só é registrado (o filtro por aluno é do banco).</summary>
    public Task<FatosProducao> CarregarFatosAsync(IReadOnlyList<Guid> profissionais, DateOnly inicio, DateOnly fim, Guid? cursoId,
        CancellationToken ct)
    {
        (IdsRecebidos, CursoRecebido, JanelaRecebida) = (profissionais, cursoId, (inicio, fim));
        return Task.FromResult(new FatosProducao(Fatos.Escalas.Where(e => profissionais.Contains(e.ProfissionalId)).ToList(), Fatos.Feriados,
            Fatos.Bloqueios.Where(b => profissionais.Contains(b.ProfissionalId)).ToList(),
            Fatos.Ocorrencias.Where(o => profissionais.Contains(o.ProfissionalId)).ToList(),
            Fatos.Pendentes.Where(p => profissionais.Contains(p.ProfissionalId)).ToList(),
            Fatos.Ajustes.Where(a => profissionais.Contains(a.ProfissionalId)).ToList()));
    }

    public Task<IReadOnlyList<AjusteProducaoDto>> ListarAjustesAsync(Guid? profissionalId, DateOnly inicio, DateOnly fim, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AjusteProducaoDto>>(Fatos.Ajustes
            .Where(a => a.Data >= inicio && a.Data <= fim && (profissionalId == null || a.ProfissionalId == profissionalId))
            .OrderByDescending(a => a.Data)
            .Select(a => new AjusteProducaoDto(a.Id, a.ProfissionalId, "Profissional", a.Data, a.Categoria, a.Categoria.Rotulo(), a.Ocorrencias,
                a.Minutos, a.Justificativa, "Autor", a.Em)).ToList());
}
