using FluentValidation;
using Nap.Application.Comum;
using Nap.Domain.Agenda;

namespace Nap.Application.Features.Agenda.Queries;

/// <summary>Sem <c>ProfissionalId</c> (visão da equipe): a escala padrão com os feriados.</summary>
public record ObterEscalaSemanaQuery(DateOnly Semana, Guid? ProfissionalId = null);

public record DiaEscalaDto(DateOnly Data, TimeOnly? Inicio, TimeOnly? Fim, bool SemAtendimento, string? Feriado);

public record EscalaSemanaDto(DateOnly Inicio, IReadOnlyList<DiaEscalaDto> Dias, string Fuso);

public class ObterEscalaSemanaValidator : AbstractValidator<ObterEscalaSemanaQuery>
{
    public ObterEscalaSemanaValidator() =>
        RuleFor(x => x.Semana).Must(d => d.DayOfWeek == DayOfWeek.Monday).WithMessage("A semana deve iniciar na segunda-feira.");
}

public sealed class ObterEscalaSemanaHandler(IValidator<ObterEscalaSemanaQuery> validador, IEscalaRepository escalas,
    IFeriadoRepository feriados, IUsuarioAtual usuario, IRelogio relogio)
{
    public async Task<EscalaSemanaDto> Handle(ObterEscalaSemanaQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        IReadOnlyList<EscalaProfissional> doProfissional = query.ProfissionalId is { } id
            ? await escalas.ListarDoProfissionalAsync(id, ct) : [];
        var daSemana = await feriados.ListarAtivosAsync(query.Semana, query.Semana.AddDays(6), ct);
        var dias = Enumerable.Range(0, 7).Select(i =>
        {
            var data = query.Semana.AddDays(i);
            // Vigência trocada no meio da semana muda a grade só a partir da data dela.
            var faixa = ServicoEscala.FaixaDe(doProfissional.FirstOrDefault(e => e.Vigente(data)), data);
            return new DiaEscalaDto(data, faixa?.Inicio, faixa?.Fim, faixa?.SemAtendimento ?? false,
                daSemana.FirstOrDefault(f => f.Data == data)?.Descricao);
        }).ToList();
        return new EscalaSemanaDto(query.Semana, dias, relogio.Fuso.Id);
    }
}
