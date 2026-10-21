using Nap.Domain.Comum;

namespace Nap.Domain.Documentos;

public class Documento : Entidade
{
    public Guid AlunoId { get; private set; }
    public Guid CursoId { get; private set; }
    public CategoriaDocumento Categoria { get; private set; }
    public string Descricao { get; private set; } = null!;
    public string NomeArquivo { get; private set; } = null!;
    public string TipoConteudo { get; private set; } = null!;
    public long TamanhoBytes { get; private set; }
    public string Sha256 { get; private set; } = null!;
    public string ChaveArmazenamento { get; private set; } = null!;
    public VisibilidadeDocumento Visibilidade { get; private set; }
    public Guid CriadoPorId { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset AtualizadoEm { get; private set; }
    public DateTimeOffset? CompartilhadoEm { get; private set; }
    public Guid? CompartilhadoPorId { get; private set; }
    public string? CompartilhadoJustificativa { get; private set; }
    public DateTimeOffset? RevogadoEm { get; private set; }
    public Guid? RevogadoPorId { get; private set; }
    public string? RevogadoJustificativa { get; private set; }
    public DateTimeOffset? ArquivadoEm { get; private set; }
    public Guid? ArquivadoPorId { get; private set; }
    public string? ArquivadoMotivo { get; private set; }

    public bool Arquivado => ArquivadoEm is not null;

    private Documento() { } // ORM

    public static Documento Criar(Guid alunoId, Guid cursoId, CategoriaDocumento categoria, string descricao,
        string nomeArquivo, string tipoConteudo, long tamanhoBytes, string sha256, string chaveArmazenamento,
        Guid autorId, DateTimeOffset agora) => new()
    {
        AlunoId = alunoId,
        CursoId = cursoId,
        Categoria = categoria,
        Descricao = ValidarTexto(descricao, "A descrição"),
        NomeArquivo = SanearNome(nomeArquivo),
        TipoConteudo = tipoConteudo,
        TamanhoBytes = tamanhoBytes,
        Sha256 = sha256,
        ChaveArmazenamento = chaveArmazenamento,
        Visibilidade = VisibilidadeDocumento.RestritoNap,
        CriadoPorId = autorId,
        CriadoEm = agora,
        AtualizadoEm = agora,
    };

    public void AlterarCategoria(CategoriaDocumento categoria)
    {
        ExigirNaoArquivado();
        Categoria = categoria;
    }

    public void Compartilhar(string justificativa, Guid autorId, DateTimeOffset agora)
    {
        ExigirNaoArquivado();
        var texto = ValidarTexto(justificativa, "A justificativa");
        if (Visibilidade == VisibilidadeDocumento.CompartilhadoCoordenacao)
            throw new DomainException("O documento já está compartilhado com a coordenação.");
        Visibilidade = VisibilidadeDocumento.CompartilhadoCoordenacao;
        CompartilhadoEm = agora;
        CompartilhadoPorId = autorId;
        CompartilhadoJustificativa = texto;
        AtualizadoEm = agora;
    }

    public void RevogarCompartilhamento(string justificativa, Guid autorId, DateTimeOffset agora)
    {
        ExigirNaoArquivado();
        var texto = ValidarTexto(justificativa, "A justificativa");
        if (Visibilidade == VisibilidadeDocumento.RestritoNap)
            throw new DomainException("O documento não está compartilhado com a coordenação.");
        Visibilidade = VisibilidadeDocumento.RestritoNap;
        RevogadoEm = agora;
        RevogadoPorId = autorId;
        RevogadoJustificativa = texto;
        AtualizadoEm = agora;
    }

    public void Arquivar(string motivo, Guid autorId, DateTimeOffset agora)
    {
        ExigirNaoArquivado();
        var texto = ValidarTexto(motivo, "O motivo");
        ArquivadoEm = agora;
        ArquivadoPorId = autorId;
        ArquivadoMotivo = texto;
        AtualizadoEm = agora;
    }

    private void ExigirNaoArquivado()
    {
        if (Arquivado) throw new DomainException("O documento está arquivado e não pode mais ser alterado.");
    }

    // Barras invertidas são tratadas como separador também fora do Windows.
    private static string SanearNome(string? nome)
    {
        var limpo = new string(Path.GetFileName((nome ?? string.Empty).Replace('\\', '/'))
            .Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (limpo.Length == 0) return "arquivo";
        if (limpo.Length <= 200) return limpo;
        var ext = Path.GetExtension(limpo);
        if (ext.Length > 20) ext = string.Empty;
        return Texto.Truncar(limpo[..^ext.Length], 200 - ext.Length) + ext;
    }

    private static string ValidarTexto(string? texto, string nome)
    {
        texto = (texto ?? string.Empty).Trim();
        if (texto.Length is < 3 or > 500)
            throw new DomainException($"{nome} deve ter de 3 a 500 caracteres.");
        return texto;
    }
}
