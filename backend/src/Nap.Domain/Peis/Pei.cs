using Nap.Domain.Comum;

namespace Nap.Domain.Peis;

public class Pei : Entidade
{
    private readonly List<VersaoPei> _versoes = [];
    private readonly List<TrocaResponsavelPei> _trocasResponsavel = [];

    public Guid AlunoId { get; private set; }
    public Guid ProfissionalResponsavelId { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }
    public Guid CriadoPorId { get; private set; }

    public IReadOnlyCollection<VersaoPei> Versoes => _versoes;

    public IReadOnlyList<TrocaResponsavelPei> TrocasResponsavel => _trocasResponsavel;

    public VersaoPei? VersaoAberta => _versoes.SingleOrDefault(v => v.Aberta);

    public bool ImpedeNovaVersao => _versoes.Any(v => VersaoPei.EstadosQueImpedemNovaVersao.Contains(v.Estado));

    public VersaoPei? VersaoPublicada => _versoes.SingleOrDefault(v => v.Estado == EstadoVersaoPei.Publicada);

    private Pei() { } // ORM

    public static Pei Criar(Guid alunoId, Guid responsavelId, ConteudoPei conteudo, Guid autorId, DateTimeOffset agora)
    {
        var pei = new Pei
        {
            AlunoId = alunoId,
            ProfissionalResponsavelId = responsavelId,
            CriadoEm = agora,
            CriadoPorId = autorId,
        };
        pei._versoes.Add(VersaoPei.Criar(pei.Id, 1, conteudo, autorId, agora));
        return pei;
    }

    public VersaoPei NovaVersao(Guid autorId, DateTimeOffset agora)
    {
        if (!PodeCriarNovaVersao(ImpedeNovaVersao))
            throw new DomainException("Já existe uma versão do PEI em andamento ou assinada aguardando publicação.");
        var ultima = _versoes.MaxBy(v => v.Numero)!;
        var nova = VersaoPei.Criar(Id, ultima.Numero + 1, ultima.Conteudo, autorId, agora);
        _versoes.Add(nova);
        return nova;
    }

    public static bool PodeCriarNovaVersao(bool impedeNovaVersao) => !impedeNovaVersao;

    public static bool EhMaisRecente(int numero, int maiorNumero) => numero == maiorNumero;

    public static bool PodePublicar(EstadoVersaoPei estado, bool maisRecente) => maisRecente && VersaoPei.PodePublicarEstado(estado);

    public void Publicar(VersaoPei versao, Guid porId, Guid cursoId, DateTimeOffset agora)
    {
        ExigirDoPei(versao);
        if (!EhMaisRecente(versao.Numero, _versoes.Max(v => v.Numero)))
            throw new DomainException("Só a versão mais recente do PEI pode ser publicada.");
        var anterior = VersaoPublicada;
        versao.Publicar(porId, cursoId, agora);
        anterior?.Arquivar($"Substituída pela versão {versao.Numero}", agora);
    }

    // A exclusão física da linha fica com o repositório.
    public void DescartarVersao(VersaoPei versao)
    {
        ExigirDoPei(versao);
        if (versao.Estado != EstadoVersaoPei.Rascunho || versao.Numero <= 1)
            throw new DomainException("Só um rascunho a partir da versão 2 pode ser descartado.");
        _versoes.Remove(versao);
    }

    // Assinaturas já colhidas (inclusive a do responsável anterior) permanecem; só a titularidade muda.
    public void TrocarResponsavel(Guid novoId, string justificativa, Guid autorId, DateTimeOffset agora)
    {
        if (novoId == Guid.Empty)
            throw new DomainException("O novo responsável é obrigatório.");
        if (novoId == ProfissionalResponsavelId)
            throw new DomainException("Este profissional já é o responsável pelo PEI.");
        justificativa = (justificativa ?? string.Empty).Trim();
        if (justificativa.Length is < 3 or > 500)
            throw new DomainException("A justificativa deve ter de 3 a 500 caracteres.");
        _trocasResponsavel.Add(new TrocaResponsavelPei(ProfissionalResponsavelId, novoId, justificativa, autorId, agora));
        ProfissionalResponsavelId = novoId;
    }

    private void ExigirDoPei(VersaoPei versao)
    {
        if (!_versoes.Contains(versao))
            throw new DomainException("A versão não pertence a este PEI.");
    }
}
