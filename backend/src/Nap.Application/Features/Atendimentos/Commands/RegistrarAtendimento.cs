using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Features.Atendimentos.Commands;

/// <summary>Com <c>compromissoId</c>: registro do agendamento. Sem ele: avulso (aluno, início, fim e justificativa).</summary>
public record RegistrarAtendimentoCommand(Guid? CompromissoId, Guid? AlunoId, Guid? ProfissionalId, DateTimeOffset? Inicio,
    DateTimeOffset? Fim, TipoAtendimento Tipo, EstadoAtendimento Estado, string? DemandaPrincipal, string? ObservacoesPrivadas,
    string? RecomendacoesEncaminhamento, string? ProximosPassos, string? JustificativaAvulso);

public class RegistrarAtendimentoValidator : AbstractValidator<RegistrarAtendimentoCommand>
{
    public RegistrarAtendimentoValidator()
    {
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.Estado).IsInEnum();
        RuleFor(x => x.DemandaPrincipal).MaximumLength(5000);
        RuleFor(x => x.ObservacoesPrivadas).MaximumLength(5000);
        RuleFor(x => x.RecomendacoesEncaminhamento).MaximumLength(5000);
        RuleFor(x => x.ProximosPassos).MaximumLength(5000);
        When(x => x.CompromissoId is null, () =>
        {
            RuleFor(x => x.AlunoId).NotNull().WithMessage("Informe o aluno.");
            RuleFor(x => x.Inicio).NotNull().WithMessage("Informe o início.");
            RuleFor(x => x.Fim).NotNull().WithMessage("Informe o término.")
                .Must((x, fim) => fim is null || x.Inicio is null || fim > x.Inicio)
                .WithMessage("O horário de término deve ser posterior ao de início.")
                .Must((x, fim) => fim is null || x.Inicio is null || fim - x.Inicio <= TimeSpan.FromHours(12))
                .WithMessage("A duração não pode ultrapassar 12 horas.");
            RuleFor(x => x.JustificativaAvulso).NotEmpty().WithMessage("Informe a justificativa do atendimento avulso.");
        });
    }
}

public sealed class RegistrarAtendimentoHandler(IValidator<RegistrarAtendimentoCommand> validador, IAtendimentoRepository atendimentos,
    IAgendaRepository agenda, IAlunoRepository alunos, IUsuarioQueries usuarios, IUsuarioAtual usuario, IRelogio relogio,
    IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache, ServicoEscala escala,
    ServicoFechamento fechamento)
{
    public async Task<Guid> Handle(RegistrarAtendimentoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        var dados = new DadosClinicos(comando.DemandaPrincipal, comando.ObservacoesPrivadas,
            comando.RecomendacoesEncaminhamento, comando.ProximosPassos);
        var id = comando.CompromissoId is { } compromissoId
            ? await RegistrarDeCompromissoAsync(compromissoId, comando, dados, ct)
            : await RegistrarAvulsoAsync(comando, dados, ct);
        await cache.InvalidarAreaAsync(ct);
        return id;
    }

    private Task<Guid> RegistrarDeCompromissoAsync(Guid compromissoId, RegistrarAtendimentoCommand comando, DadosClinicos dados,
        CancellationToken ct) =>
        uow.EmTransacaoAsync(async tct =>
        {
            var compromisso = await agenda.ObterAsync(compromissoId, tct) ?? throw new NotFoundException("Compromisso");
            if (!usuario.EhCoordenacao() && compromisso.ProfissionalId != usuario.Id) throw new ForbiddenException();
            await fechamento.ExigirSemanaAbertaAsync(compromisso.ProfissionalId, compromisso.Intervalo.Inicio, tct);
            if (await atendimentos.ExisteParaCompromissoAsync(compromissoId, tct))
                throw new ConflictException("Este agendamento já possui atendimento registrado");

            var atendimento = Atendimento.RegistrarDeCompromisso(compromisso, comando.Tipo, comando.Estado, dados,
                usuario.Id, relogio.Agora);
            atendimentos.Adicionar(atendimento);
            auditoria.Registrar("atendimento.registro", "Atendimento", atendimento.Id);
            await uow.SalvarAsync(tct);
            return atendimento.Id;
        }, ct);

    private async Task<Guid> RegistrarAvulsoAsync(RegistrarAtendimentoCommand comando, DadosClinicos dados, CancellationToken ct)
    {
        var profissionalId = await usuarios.ResolverProfissionalAsync(usuario, comando.ProfissionalId, ct);
        var alunoId = comando.AlunoId!.Value;
        if (await alunos.ObterAsync(alunoId, ct) is null)
            throw Validacao.ErroDeCampo("alunoId", "Aluno inexistente.");

        var intervalo = IntervaloHorario.Criar(comando.Inicio!.Value, comando.Fim!.Value);
        var atendimento = Atendimento.RegistrarAvulso(alunoId, profissionalId, intervalo, comando.Tipo, comando.Estado, dados,
            comando.JustificativaAvulso!, usuario.Id, relogio.Agora);

        return await uow.EmTransacaoAsync(async tct =>
        {
            await fechamento.ExigirSemanaAbertaAsync(profissionalId, intervalo.Inicio, tct);
            await agenda.GarantirSemConflitoAsync(profissionalId, alunoId, intervalo, null, true, tct);
            await escala.ExigirDentroDaEscalaAsync(profissionalId, comando.Inicio!.Value, tct);
            atendimentos.Adicionar(atendimento);
            auditoria.Registrar("atendimento.registro", "Atendimento", atendimento.Id);
            await uow.SalvarAsync(tct);
            return atendimento.Id;
        }, ct);
    }
}
