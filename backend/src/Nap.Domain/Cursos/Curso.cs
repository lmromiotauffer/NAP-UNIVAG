using Nap.Domain.Comum;

namespace Nap.Domain.Cursos;

public class Curso : Entidade
{
    public string Nome { get; private set; } = null!;
    public bool Ativo { get; private set; }

    private Curso() { } // ORM

    public static Curso Criar(string nome)
    {
        return new Curso { Nome = NomeValido(nome), Ativo = true };
    }

    public void Renomear(string nome) => Nome = NomeValido(nome);

    public void Inativar() => Ativo = false;

    public void Reativar() => Ativo = true;

    private static string NomeValido(string nome)
    {
        nome = (nome ?? string.Empty).Trim();
        if (nome.Length is < 2 or > 120)
            throw new DomainException("O nome do curso deve ter de 2 a 120 caracteres.");
        return nome;
    }
}
