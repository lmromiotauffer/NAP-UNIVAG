using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Agenda;

namespace Nap.Application.Features.Agenda.Commands;

public record FaixaEscalaInput(DayOfWeek Dia, TimeOnly Inicio, TimeOnly Fim, bool SemAtendimento);

public record CriarEscalaCommand(Guid ProfissionalId, DateOnly VigenciaInicio, DateOnly? VigenciaFim, IReadOnlyList<FaixaEscalaInput> Faixas);

public class CriarEscalaValidator : AbstractValidator<CriarEscalaCommand>
{
    public CriarEscalaValidator()
    {
        RuleFor(x => x.ProfissionalId).NotEmpty().WithMessage("Informe o profissional.");
        RuleFor(x => x.VigenciaInicio).NotEqual(default(DateOnly)).WithMessage("Informe o início da vigência.");
        RuleFor(x => x.VigenciaFim).Must((x, fim) => fim is null || fim >= x.VigenciaInicio)
            .WithMessage("O fim da vigência não pode ser anterior ao início.");
        RuleFor(x => x.Faixas).NotEmpty().WithMessage("Informe ao menos uma faixa na escala.")
            .Must(fs => fs is null || fs.Where(f => f is not null).GroupBy(f => f.Dia).All(g => g.Count() == 1))
            .WithMessage("A escala pode ter no máximo uma faixa por dia.");
        RuleForEach(x => x.Faixas).NotNull().ChildRules(f =>
        {
            f.RuleFor(x => x.Dia).IsInEnum();
            f.RuleFor(x => x.Inicio).Must(MultiploDe15).WithMessage(MensagemMultiplo);
            f.RuleFor(x => x.Fim).GreaterThan(x => x.Inicio).WithMessage("O fim da faixa deve ser depois do início.")
                .Must(MultiploDe15).WithMessage(MensagemMultiplo);
        });
    }

    private const string MensagemMultiplo = "Os horários da faixa devem ser múltiplos de 15 minutos.";

    private static bool MultiploDe15(TimeOnly t) => t.Ticks % TimeSpan.FromMinutes(15).Ticks == 0;
}

public sealed class CriarEscalaHandler(IValidator<CriarEscalaCommand> validador, IEscalaRepository escalas, IAgendaRepository agenda,
    IUsuarioQueries usuarios, IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public const string MensagemSobreposicao = "A vigência se sobrepõe a outra escala deste profissional.";

    public async Task<Guid> Handle(CriarEscalaCommand comando, CancellationToken ct = default)
    {
        RegrasAlcada.ExigirAdministracao(usuario);
        await validador.ValidarAsync(comando, ct);
        if (!await usuarios.EhProfissionalAtivoAsync(comando.ProfissionalId, ct))
            throw Validacao.ErroDeCampo("profissionalId", "O profissional deve ser um membro ativo da equipe NAP.");

        var escala = EscalaProfissional.Criar(comando.ProfissionalId, comando.VigenciaInicio, comando.VigenciaFim,
            comando.Faixas.Select(f => new FaixaEscala(f.Dia, f.Inicio, f.Fim, f.SemAtendimento)).ToList());
        return await uow.EmTransacaoAsync(async tct =>
        {
            await ExigirSemSobreposicaoAsync(escalas, agenda, escala, tct);
            escalas.Adicionar(escala);
            auditoria.Registrar("escala.criar", "EscalaProfissional", escala.Id);
            await uow.SalvarAsync(tct);
            return escala.Id;
        }, ct);
    }

    /// <summary>Usa a trava da agenda do profissional para serializar a checagem com outras gravações dele.</summary>
    internal static async Task ExigirSemSobreposicaoAsync(IEscalaRepository escalas, IAgendaRepository agenda,
        EscalaProfissional escala, CancellationToken ct)
    {
        await agenda.BloquearAgendasAsync(escala.ProfissionalId, null, ct);
        var outras = await escalas.ListarDoProfissionalAsync(escala.ProfissionalId, ct);
        if (outras.Any(o => o.Id != escala.Id && o.SobrepoeA(escala)))
            throw new ConflictException(MensagemSobreposicao);
    }
}
