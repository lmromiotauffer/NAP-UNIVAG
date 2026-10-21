using Nap.Domain.Comum;

namespace Nap.Domain.Agenda;

public class Feriado : Entidade
{
    public DateOnly Data { get; private set; }
    public string Descricao { get; private set; } = null!;
    public bool Ativo { get; private set; }

    private Feriado() { } // ORM

    public static Feriado Criar(DateOnly data, string descricao)
    {
        descricao = (descricao ?? string.Empty).Trim();
        if (descricao.Length is < 3 or > 120)
            throw new DomainException("A descrição do feriado deve ter de 3 a 120 caracteres.");
        return new Feriado { Data = data, Descricao = descricao, Ativo = true };
    }

    public void Inativar() => Ativo = false;
}
