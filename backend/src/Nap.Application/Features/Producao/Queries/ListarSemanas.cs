using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Producao.Queries;

public record ListarSemanasQuery(Guid? ProfissionalId, DateOnly Inicio, DateOnly Fim);

public class ListarSemanasValidator : AbstractValidator<ListarSemanasQuery>
{
    public ListarSemanasValidator()
    {
        RuleFor(x => x.Fim).GreaterThanOrEqualTo(x => x.Inicio).WithMessage("O fim deve ser igual ou posterior ao início.")
            .Must((x, fim) => fim.DayNumber - x.Inicio.DayNumber <= 366).WithMessage("O período não pode passar de um ano.");
    }
}

public sealed class ListarSemanasHandler(IValidator<ListarSemanasQuery> validador, IProducaoQueries consultas,
    IUsuarioQueries usuarios, IUsuarioAtual usuario, IRelogio relogio)
{
    public async Task<IReadOnlyList<SemanaProducaoDto>> Handle(ListarSemanasQuery consulta, CancellationToken ct = default)
    {
        await validador.ValidarAsync(consulta, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        IReadOnlyList<ProfissionalDto> profissionais;
        if (!usuario.EhCoordenacao())
        {
            // Profissional só enxerga a própria produção; o filtro recebido é ignorado.
            var eu = await usuarios.ObterResumoAsync(usuario.Id, ct) ?? throw new NotFoundException("Profissional");
            profissionais = [new ProfissionalDto(eu.Id, eu.Nome)];
        }
        else
        {
            // Ativos e também inativos com fechamento ou fatos na janela.
            profissionais = (await usuarios.ListarProfissionaisDaProducaoAsync(ServicoFechamento.SegundaDe(consulta.Inicio),
                    ServicoFechamento.SegundaDe(consulta.Fim).AddDays(6),
                    relogio.Fuso, consulta.ProfissionalId, ct))
                .Select(p => new ProfissionalDto(p.Id, p.Nome)).ToList();
            if (consulta.ProfissionalId is not null && profissionais.Count == 0) throw new NotFoundException("Profissional");
        }
        return await consultas.ListarSemanasAsync(profissionais, consulta.Inicio, consulta.Fim, ct);
    }
}
