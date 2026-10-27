using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Agenda;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Features.Agenda.Commands;

/// <param name="ParticipantesIds">Compromisso da equipe: lista completa de participantes (o sistema não inclui ninguém sozinho).</param>
/// <param name="TodaEquipe">Compromisso da equipe com todos os profissionais ativos da equipe NAP.</param>
public record CriarCompromissoCommand(TipoCompromisso Tipo, string Titulo, DateTimeOffset Inicio, DateTimeOffset Fim,
    Guid? ProfissionalId, Guid? AlunoId, string? Descricao, IReadOnlyList<Guid>? ParticipantesIds = null, bool TodaEquipe = false)
{
    public bool EhGrupo => ParticipantesIds is { Count: > 0 } || TodaEquipe;
}

public class CriarCompromissoValidator : AbstractValidator<CriarCompromissoCommand>
{
    public CriarCompromissoValidator()
    {
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.Titulo).NotEmpty().WithMessage("Informe o título.").MaximumLength(120);
        RuleFor(x => x.Descricao).MaximumLength(2000);
        RuleFor(x => x.Fim).IntervaloValido(x => x.Inicio);
        When(x => x.EhGrupo, () =>
        {
            RuleFor(x => x.ParticipantesIds)
                .Must((x, _) => x.Tipo.PermiteGrupo()).WithMessage("Só reuniões e lembretes aceitam participantes.")
                .Must((x, ids) => ids is not { Count: > 0 } || !x.TodaEquipe).WithMessage("Informe os participantes ou toda a equipe, não ambos.");
            RuleFor(x => x.ProfissionalId).Null().WithMessage("Num compromisso da equipe, informe os participantes, não o profissional.");
            RuleFor(x => x.AlunoId).Null().WithMessage("Compromisso da equipe não tem aluno.");
        });
    }
}

public sealed class CriarCompromissoHandler(IValidator<CriarCompromissoCommand> validador, IAgendaRepository agenda,
    IAlunoRepository alunos, IUsuarioQueries usuarios, IUsuarioAtual usuario, IAuditoria auditoria,
    IUnidadeDeTrabalho uow, ICacheConsultas cache, ServicoEscala escala,
    ServicoFechamento fechamento)
{
    public async Task<Guid> Handle(CriarCompromissoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();
        if (comando.EhGrupo) return await CriarGrupoAsync(comando, ct);

        var profissionalId = await usuarios.ResolverProfissionalAsync(usuario, comando.ProfissionalId, ct);
        if (comando.AlunoId is { } alunoId && await alunos.ObterAsync(alunoId, ct) is null)
            throw Validacao.ErroDeCampo("alunoId", "Aluno inexistente.");

        var intervalo = IntervaloHorario.Criar(comando.Inicio, comando.Fim);
        var compromisso = Compromisso.Criar(comando.Tipo, comando.Titulo, intervalo, profissionalId, comando.AlunoId,
            comando.Descricao, usuario.Id);

        await uow.EmTransacaoAsync(async tct =>
        {
            await fechamento.ExigirSemanaAbertaAsync(profissionalId, intervalo, tct);
            await agenda.GarantirSemConflitoAsync(profissionalId, comando.AlunoId, intervalo, null, comando.Tipo.OcupaHorario(), tct,
                listarConflitos: comando.Tipo == TipoCompromisso.Bloqueio);
            // Sob a trava da agenda do profissional, que também serializa as gravações de escala dele.
            if (comando.Tipo.SegueEscala()) await escala.ExigirDentroDaEscalaAsync(profissionalId, comando.Inicio, tct);
            agenda.Adicionar(compromisso);
            auditoria.Registrar("agenda.criacao", "Compromisso", compromisso.Id);
            await uow.SalvarAsync(tct);
            return compromisso.Id;
        }, ct);
        await cache.InvalidarAreaAsync(ct);
        return compromisso.Id;
    }

    /// <summary>Uma instância por participante, todas ou nenhuma; devolve a de quem cria (ou a primeira, se a coordenação não participa).</summary>
    private async Task<Guid> CriarGrupoAsync(CriarCompromissoCommand comando, CancellationToken ct)
    {
        var ativos = (await usuarios.ListarProfissionaisAsync(false, ct)).Select(p => p.Id).ToHashSet();
        var participantes = GrupoCompromisso.EmOrdemDeTrava(comando.TodaEquipe ? ativos : comando.ParticipantesIds!);
        if (participantes.Count > GrupoCompromisso.MaximoParticipantes)
            throw Validacao.ErroDeCampo("participantesIds", $"Informe no máximo {GrupoCompromisso.MaximoParticipantes} participantes.");
        if (!participantes.All(ativos.Contains))
            throw Validacao.ErroDeCampo("participantesIds", "Os participantes devem ser membros ativos da equipe NAP.");
        if (!usuario.EhCoordenacao() && !participantes.Contains(usuario.Id))
            throw Validacao.ErroDeCampo("participantesIds", "Inclua-se entre os participantes; só a coordenação cria compromisso da equipe sem participar.");

        var intervalo = IntervaloHorario.Criar(comando.Inicio, comando.Fim);
        var grupoId = Guid.CreateVersion7();
        var instancias = participantes.Select(p => Compromisso.Criar(comando.Tipo, comando.Titulo, intervalo, p, null, comando.Descricao,
            usuario.Id, grupoId)).ToList();

        await uow.EmTransacaoAsync(async tct =>
        {
            // Todas as travas de fechamento (em ordem) antes de todas as de agenda: intercalar por participante causa deadlock.
            foreach (var p in participantes) await fechamento.ExigirSemanaAbertaAsync(p, intervalo, tct);
            await agenda.GarantirGrupoSemConflitoAsync([.. participantes.Select(p => (p, (Guid?)null))], intervalo,
                comando.Tipo.OcupaHorario(), tct);
            // shortcut: escala e feriado são consultados por participante (até ~5 idas ao banco cada, 50 no máximo) com as travas
            // presas; se a criação do grupo ficar lenta, carregar feriado, autorizações e escalas de todos numa consulta só.
            if (comando.Tipo.SegueEscala())
                foreach (var p in participantes) await escala.ExigirDentroDaEscalaAsync(p, comando.Inicio, tct);
            foreach (var c in instancias)
            {
                agenda.Adicionar(c);
                auditoria.Registrar("agenda.criacao", "Compromisso", c.Id);
            }
            await uow.SalvarAsync(tct);
            return grupoId;
        }, ct);
        await cache.InvalidarAreaAsync(ct);
        return (instancias.FirstOrDefault(c => c.ProfissionalId == usuario.Id) ?? instancias[0]).Id;
    }
}
