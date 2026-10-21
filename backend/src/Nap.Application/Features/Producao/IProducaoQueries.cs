using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Producao;

public interface IProducaoQueries
{
    /// <summary>Pendentes de classificação do profissional na semana (mesma definição de "sem desfecho" do painel).</summary>
    Task<int> ContarPendentesAsync(Guid profissionalId, DateOnly semana, CancellationToken ct);

    /// <summary>Uma linha por profissional e por segunda-feira das semanas que cruzam [<paramref name="inicio"/>, <paramref name="fim"/>].</summary>
    Task<IReadOnlyList<SemanaProducaoDto>> ListarSemanasAsync(IReadOnlyList<ProfissionalDto> profissionais, DateOnly inicio, DateOnly fim,
        CancellationToken ct);

    /// <summary>
    /// Fatos dos profissionais com início (data civil) em [<paramref name="inicio"/>, <paramref name="fim"/>]: escalas, feriados ativos,
    /// bloqueios, ocorrências concluídas, pendentes e ajustes. Com <paramref name="cursoId"/>, só atendimentos de alunos do curso,
    /// atividades do curso e pendentes de alunos do curso (capacidade, reuniões e ajustes não se recortam por curso).
    /// </summary>
    Task<FatosProducao> CarregarFatosAsync(IReadOnlyList<Guid> profissionais, DateOnly inicio, DateOnly fim, Guid? cursoId,
        CancellationToken ct);

    /// <summary>Ajustes com data em [<paramref name="inicio"/>, <paramref name="fim"/>], com os nomes do profissional e do autor, do mais recente ao mais antigo.</summary>
    Task<IReadOnlyList<AjusteProducaoDto>> ListarAjustesAsync(Guid? profissionalId, DateOnly inicio, DateOnly fim, CancellationToken ct);
}
