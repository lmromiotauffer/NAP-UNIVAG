using FluentValidation;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Comum;

/// <summary>Regra de entrada do intervalo, a mesma de <see cref="IntervaloHorario.Criar"/> (minutos inteiros), como erro no campo do fim.</summary>
public static class ValidacaoIntervalo
{
    public const string MensagemOrdem = "O horário de término deve ser posterior ao de início.";
    public const string MensagemDuracao = "A duração não pode ultrapassar 12 horas.";

    public static IRuleBuilderOptions<T, DateTimeOffset> IntervaloValido<T>(this IRuleBuilder<T, DateTimeOffset> fim,
        Func<T, DateTimeOffset> inicio) =>
        fim.Must((x, f) => Duracao(inicio(x), f) > TimeSpan.Zero).WithMessage(MensagemOrdem)
            .Must((x, f) => Duracao(inicio(x), f) <= TimeSpan.FromHours(12)).WithMessage(MensagemDuracao);

    /// <summary>Início ou fim ausentes passam: a obrigatoriedade é regra própria do validador.</summary>
    public static IRuleBuilderOptions<T, DateTimeOffset?> IntervaloValido<T>(this IRuleBuilder<T, DateTimeOffset?> fim,
        Func<T, DateTimeOffset?> inicio) =>
        fim.Must((x, f) => f is null || inicio(x) is null || Duracao(inicio(x)!.Value, f.Value) > TimeSpan.Zero).WithMessage(MensagemOrdem)
            .Must((x, f) => f is null || inicio(x) is null || Duracao(inicio(x)!.Value, f.Value) <= TimeSpan.FromHours(12))
            .WithMessage(MensagemDuracao);

    private static TimeSpan Duracao(DateTimeOffset inicio, DateTimeOffset fim) => IntervaloHorario.Minuto(fim) - IntervaloHorario.Minuto(inicio);
}
