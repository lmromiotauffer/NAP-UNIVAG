using Nap.Domain.Comum;

namespace Nap.Domain.Peis;

public class VersaoPei : Entidade
{
    private readonly List<AssinaturaPei> _assinaturas = [];
    private readonly List<CienciaPei> _ciencias = [];

    public Guid PeiId { get; private set; }
    public int Numero { get; private set; }
    public ConteudoPei Conteudo { get; private set; } = null!;
    public EstadoVersaoPei Estado { get; private set; }
    public string? HashConteudo { get; private set; }
    public DateTimeOffset CriadaEm { get; private set; }
    public Guid CriadaPorId { get; private set; }
    public DateTimeOffset AtualizadaEm { get; private set; }
    public DateTimeOffset? EnviadaRevisaoEm { get; private set; }
    public DateTimeOffset? AprovadaEm { get; private set; }
    public Guid? AprovadaPorId { get; private set; }
    public DateTimeOffset? DevolvidaEm { get; private set; }
    public string? JustificativaDevolucao { get; private set; }
    public DateTimeOffset? PublicadaEm { get; private set; }
    public Guid? PublicadaPorId { get; private set; }
    public Guid? CursoPublicacaoId { get; private set; }
    public DateTimeOffset? ArquivadaEm { get; private set; }
    public string? MotivoArquivamento { get; private set; }

    /// <summary>Todas, inclusive as invalidadas por uma devolução; as que valem estão em <see cref="AssinaturaValida"/>.</summary>
    public IReadOnlyCollection<AssinaturaPei> Assinaturas => _assinaturas;
    public IReadOnlyCollection<CienciaPei> Ciencias => _ciencias;

    public static readonly EstadoVersaoPei[] EstadosAbertos =
        [EstadoVersaoPei.Rascunho, EstadoVersaoPei.EmRevisao, EstadoVersaoPei.AguardandoAssinaturas];

    public bool Aberta => EstadosAbertos.Contains(Estado);

    /// <summary>Abertos e <c>Assinada</c>: com uma delas não se cria nova versão (a assinada só sai do caminho publicada,
    /// e só a mais recente publica; uma nova versão a deixaria presa).</summary>
    public static readonly EstadoVersaoPei[] EstadosQueImpedemNovaVersao = [.. EstadosAbertos, EstadoVersaoPei.Assinada];

    // Condições de estado de cada operação: os métodos abaixo as usam como guarda e a consulta de leitura as reaproveita
    // para os botões, então uma mudança de regra vale nos dois lugares.
    public static bool PodeEditar(EstadoVersaoPei e) => e == EstadoVersaoPei.Rascunho;
    public static bool PodeEnviarRevisao(EstadoVersaoPei e) => e == EstadoVersaoPei.Rascunho;
    public static bool PodeAprovar(EstadoVersaoPei e) => e == EstadoVersaoPei.EmRevisao;
    public static bool PodeDevolver(EstadoVersaoPei e) => e is EstadoVersaoPei.EmRevisao or EstadoVersaoPei.AguardandoAssinaturas;
    public static bool PodeRegistrarAssinatura(EstadoVersaoPei e) => e == EstadoVersaoPei.AguardandoAssinaturas;
    public static bool PodePublicarEstado(EstadoVersaoPei e) => e == EstadoVersaoPei.Assinada;
    public static bool PodeArquivar(EstadoVersaoPei e) => e == EstadoVersaoPei.Publicada;
    public static bool PodeRegistrarCiencia(EstadoVersaoPei e) => e == EstadoVersaoPei.Publicada;

    public bool TodasAssinaturas => Enum.GetValues<PapelAssinatura>().All(p => AssinaturaValida(p) is not null);

    public AssinaturaPei? AssinaturaValida(PapelAssinatura papel) => _assinaturas.SingleOrDefault(a => a.Valida && a.Papel == papel);

    private VersaoPei() { } // ORM

    internal static VersaoPei Criar(Guid peiId, int numero, ConteudoPei conteudo, Guid autorId, DateTimeOffset agora) =>
        new()
    {
        PeiId = peiId,
        Numero = numero,
        Conteudo = conteudo ?? throw new DomainException("O conteúdo do PEI é obrigatório."),
        Estado = EstadoVersaoPei.Rascunho,
        CriadaEm = agora,
        CriadaPorId = autorId,
        AtualizadaEm = agora,
    };

    public void AtualizarConteudo(ConteudoPei conteudo, DateTimeOffset agora)
    {
        if (!PodeEditar(Estado)) throw new DomainException("O conteúdo só pode ser alterado em rascunho.");
        Conteudo = conteudo ?? throw new DomainException("O conteúdo do PEI é obrigatório.");
        AtualizadaEm = agora;
    }

    public void EnviarParaRevisao(DateTimeOffset agora)
    {
        if (!PodeEnviarRevisao(Estado)) throw new DomainException("Só um rascunho pode ser enviado para revisão.");
        if (!Conteudo.TemConteudoPedagogico)
            throw new DomainException("Preencha ao menos um campo pedagógico antes de enviar para revisão.");
        Estado = EstadoVersaoPei.EmRevisao;
        EnviadaRevisaoEm = agora;
        AtualizadaEm = agora;
    }

    public void AprovarRevisao(Guid aprovadorId, DateTimeOffset agora)
    {
        if (!PodeAprovar(Estado)) throw new DomainException("Só uma versão em revisão pode ser aprovada.");
        Estado = EstadoVersaoPei.AguardandoAssinaturas;
        HashConteudo = CalculadoraHashPei.Calcular(Conteudo);
        AprovadaEm = agora;
        AprovadaPorId = aprovadorId;
        AtualizadaEm = agora;
    }

    // Também vale em AguardandoAssinaturas: o conteúdo volta a ser editável, então o hash
    // congelado e as assinaturas já colhidas deixam de valer (invalidadas, não apagadas: são a prova do que foi assinado).
    public void Devolver(string justificativa, DateTimeOffset agora)
    {
        if (!PodeDevolver(Estado))
            throw new DomainException("Só uma versão em revisão ou aguardando assinaturas pode ser devolvida.");
        var texto = (justificativa ?? string.Empty).Trim();
        if (texto.Length is < 3 or > 500)
            throw new DomainException("A justificativa deve ter de 3 a 500 caracteres.");
        Estado = EstadoVersaoPei.Rascunho;
        DevolvidaEm = agora;
        JustificativaDevolucao = texto;
        HashConteudo = null;
        AprovadaEm = null;
        AprovadaPorId = null;
        foreach (var a in _assinaturas) a.Invalidar(agora);
        AtualizadaEm = agora;
    }

    public void RegistrarAssinatura(PapelAssinatura papel, Guid? signatarioId, string nome, string hashConteudo,
        string evidencia, DateTimeOffset agora, string metodo = "Demonstrativa")
    {
        if (!PodeRegistrarAssinatura(Estado)) throw new DomainException("A versão não está aguardando assinaturas.");
        if (!string.Equals(hashConteudo, HashConteudo, StringComparison.Ordinal))
            throw new DomainException("O hash assinado não corresponde ao conteúdo da versão.");
        if (AssinaturaValida(papel) is not null)
            throw new DomainException("Esta versão já foi assinada neste papel.");
        var nomeLimpo = (nome ?? string.Empty).Trim();
        if (nomeLimpo.Length is < 1 or > 200)
            throw new DomainException("O nome do signatário deve ter de 1 a 200 caracteres.");
        if (string.IsNullOrWhiteSpace(evidencia))
            throw new DomainException("A evidência da assinatura é obrigatória.");
        _assinaturas.Add(AssinaturaPei.Criar(papel, signatarioId, nomeLimpo, hashConteudo, metodo, evidencia, agora));
        if (TodasAssinaturas) Estado = EstadoVersaoPei.Assinada;
        AtualizadaEm = agora;
    }

    // Só pelo agregado (Pei.Publicar), que garante a versão mais recente e arquiva a anterior.
    internal void Publicar(Guid porId, Guid cursoId, DateTimeOffset agora)
    {
        if (!PodePublicarEstado(Estado)) throw new DomainException("Só uma versão assinada pode ser publicada.");
        Estado = EstadoVersaoPei.Publicada;
        PublicadaEm = agora;
        PublicadaPorId = porId;
        CursoPublicacaoId = cursoId;
        AtualizadaEm = agora;
    }

    public void Arquivar(string motivo, DateTimeOffset agora)
    {
        if (!PodeArquivar(Estado)) throw new DomainException("Só uma versão publicada pode ser arquivada.");
        var texto = (motivo ?? string.Empty).Trim();
        if (texto.Length is < 3 or > 500)
            throw new DomainException("O motivo deve ter de 3 a 500 caracteres.");
        Estado = EstadoVersaoPei.Arquivada;
        ArquivadaEm = agora;
        MotivoArquivamento = texto;
        AtualizadaEm = agora;
    }

    public void RegistrarCiencia(Guid coordenadorId, DateTimeOffset agora)
    {
        if (!PodeRegistrarCiencia(Estado)) throw new DomainException("A ciência só pode ser registrada em uma versão publicada.");
        if (_ciencias.Any(c => c.CoordenadorId == coordenadorId)) return;
        _ciencias.Add(CienciaPei.Criar(coordenadorId, agora));
    }
}
