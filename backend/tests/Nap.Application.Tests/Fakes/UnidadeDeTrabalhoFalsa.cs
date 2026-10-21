using Nap.Application.Comum;

namespace Nap.Application.Tests.Fakes;

public sealed class UnidadeDeTrabalhoFalsa(List<string>? ordem = null) : IUnidadeDeTrabalho
{
    public int Salvamentos { get; private set; }

    /// <summary>Simula falha do banco no SaveChanges.</summary>
    public bool FalharAoSalvar { get; init; }

    /// <summary>Simula conflito (concorrência ou unicidade) com esta mensagem em todo SaveChanges.</summary>
    public string? ConflitoAoSalvar { get; init; }

    public Task SalvarAsync(CancellationToken ct)
    {
        if (ConflitoAoSalvar is not null) throw new ConflictException(ConflitoAoSalvar);
        if (FalharAoSalvar) throw new InvalidOperationException("Falha simulada do banco.");
        Salvamentos++;
        ordem?.Add("salvar");
        return Task.CompletedTask;
    }

    /// <summary>Null aceita qualquer versão; preenchida, exige igualdade.</summary>
    public string? VersaoAtual { get; set; }
    public List<string> VersoesExigidas { get; } = [];

    public void ExigirVersao(object entidade, string versaoEsperada)
    {
        VersoesExigidas.Add(versaoEsperada);
        if (VersaoAtual is not null && VersaoAtual != versaoEsperada)
            throw new ConflictException("O registro foi alterado por outra pessoa. Recarregue e tente novamente");
    }

    public Task<T> EmTransacaoAsync<T>(Func<CancellationToken, Task<T>> operacao, CancellationToken ct)
    {
        ordem?.Add("transacao");
        return operacao(ct);
    }
}

public sealed class AuditoriaFalsa : IAuditoria
{
    public List<(string Acao, string Recurso, Guid? RecursoId)> Registros { get; } = [];

    public void Registrar(string acao, string recurso, Guid? recursoId) =>
        Registros.Add((acao, recurso, recursoId));

    public Task RegistrarNegadoAsync(string acao, string recurso, Guid? recursoId, CancellationToken ct, bool semUsuario = false) =>
        Task.CompletedTask;
}
