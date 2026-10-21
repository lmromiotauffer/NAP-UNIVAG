namespace Nap.Infrastructure.Notificacoes;

/// <summary>Ligada à seção <c>Nap:Outbox</c>.</summary>
public sealed class OpcoesOutbox
{
    public int IntervaloSegundos { get; set; } = 5;

    public static bool Valida(OpcoesOutbox o) => o.IntervaloSegundos > 0;
}
