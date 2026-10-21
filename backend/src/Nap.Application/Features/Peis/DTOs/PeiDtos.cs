using Nap.Domain.Peis;

namespace Nap.Application.Features.Peis;

public record VersaoResumoDto(Guid Id, int Numero, EstadoVersaoPei Estado, DateOnly VigenciaInicio, DateOnly VigenciaFim,
    DateTimeOffset? PublicadaEm, bool CienciaRegistradaPorMim, bool ForaDaVigencia);

public record PeiResumoDto(Guid Id, string Responsavel, IReadOnlyList<VersaoResumoDto> Versoes);

/// <summary><c>DemandaInterna</c> chega <c>null</c> ao coordenador de curso: não faz parte do documento assinado.</summary>
public record ConteudoPeiDto(string? DemandaInterna, string? NecessidadesEducacionais, string? Adaptacoes,
    string? OrientacoesProfessores, string? RecursosApoio, string? CuidadosPedagogicos, DateOnly VigenciaInicio,
    DateOnly VigenciaFim);

public record AssinaturaDto(PapelAssinatura Papel, string Nome, DateTimeOffset Em, string Metodo);

public record CienciaDto(string Coordenador, DateTimeOffset Em);

public record AcoesVersaoDto(bool PodeEditar, bool PodeEnviarRevisao, bool PodeAprovar, bool PodeDevolver,
    bool PodeAssinarComoProfissional, bool PodeGerarAcessoAluno, bool PodePublicar, bool PodeArquivar,
    bool PodeRegistrarCiencia, bool PodeCriarNovaVersao);

public record VersaoPeiDetalheDto(Guid Id, Guid PeiId, Guid AlunoId, string Aluno, string Matricula, string Curso,
    string Responsavel, int Numero, EstadoVersaoPei Estado, ConteudoPeiDto Conteudo, string? HashConteudo,
    IReadOnlyList<AssinaturaDto> Assinaturas, IReadOnlyList<CienciaDto> Ciencias, DateTimeOffset? PublicadaEm,
    string? PublicadaPor, AcoesVersaoDto Acoes, string Versao, bool ForaDaVigencia);

/// <summary>Contagens de versões do PEI no escopo do perfil.</summary>
public record PeisPainelDto(int EmElaboracao, int AguardandoAssinaturas, int PublicadosSemCiencia);

/// <summary>Aviso calculado, nunca altera estado: só a versão publicada, e o último dia da vigência ainda vale.</summary>
public static class VigenciaPei
{
    public static bool ForaDaVigencia(EstadoVersaoPei estado, DateOnly fim, DateOnly hoje) =>
        estado == EstadoVersaoPei.Publicada && fim < hoje;
}
