using Nap.Domain.Comum;

namespace Nap.Domain.Producao;

/// <summary>Correção manual da produção; não entra na capacidade nem na identidade. A semana aberta é exigida pelo handler.</summary>
public class AjusteProducao : Entidade
{
    public Guid ProfissionalId { get; private set; }
    public DateOnly Data { get; private set; }
    public CategoriaProducao Categoria { get; private set; }
    public int? Ocorrencias { get; private set; }
    public int? Minutos { get; private set; }
    public string Justificativa { get; private set; } = null!;
    public Guid AutorId { get; private set; }
    public DateTimeOffset Em { get; private set; }

    private AjusteProducao() { } // ORM

    public static AjusteProducao Criar(Guid profissionalId, DateOnly data, CategoriaProducao categoria, int? ocorrencias,
        int? minutos, string justificativa, Guid autorId, DateTimeOffset em)
    {
        if (categoria == CategoriaProducao.Ociosidade)
            throw new DomainException("Horários disponíveis sem utilização não aceitam ajuste.");
        if ((ocorrencias ?? 0) == 0 && (minutos ?? 0) == 0)
            throw new DomainException("O ajuste deve alterar as ocorrências ou os minutos.");
        if (ocorrencias is < -1000 or > 1000)
            throw new DomainException("As ocorrências do ajuste devem estar entre -1000 e 1000.");
        if (minutos is < -10000 or > 10000)
            throw new DomainException("Os minutos do ajuste devem estar entre -10000 e 10000.");
        justificativa = (justificativa ?? string.Empty).Trim();
        if (justificativa.Length is < 5 or > 500)
            throw new DomainException("A justificativa do ajuste é obrigatória e deve ter de 5 a 500 caracteres.");

        return new AjusteProducao
        {
            ProfissionalId = profissionalId,
            Data = data,
            Categoria = categoria,
            Ocorrencias = ocorrencias,
            Minutos = minutos,
            Justificativa = justificativa,
            AutorId = autorId,
            Em = em,
        };
    }
}
