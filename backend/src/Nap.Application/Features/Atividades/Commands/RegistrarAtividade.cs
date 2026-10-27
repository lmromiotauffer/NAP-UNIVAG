using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Cursos;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Atividades;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Features.Atividades.Commands;

/// <summary>Com <c>compromissoId</c>: registro do agendamento. Sem ele: avulsa (exige justificativa).</summary>
public record RegistrarAtividadeCommand(Guid? CompromissoId, Guid? ProfissionalId, DateTimeOffset Inicio, DateTimeOffset Fim,
    Guid? CursoId, TipoAtividade Tipo, string Descricao, string? JustificativaAvulso);

public class RegistrarAtividadeValidator : AbstractValidator<RegistrarAtividadeCommand>
{
    public RegistrarAtividadeValidator()
    {
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.Descricao).NotEmpty().WithMessage("Informe a descrição da atividade.")
            .Must(d => d is null || d.Trim().Length is >= 3 and <= 2000).WithMessage("A descrição deve ter de 3 a 2000 caracteres.");
        RuleFor(x => x.Fim).IntervaloValido(x => x.Inicio);
        When(x => x.CompromissoId is null, () =>
            RuleFor(x => x.JustificativaAvulso).NotEmpty().WithMessage("Informe a justificativa da atividade avulsa.")
                .Must(j => j is null || j.Trim().Length is >= 5 and <= 500).WithMessage("A justificativa deve ter de 5 a 500 caracteres."));
    }
}

public sealed class RegistrarAtividadeHandler(IValidator<RegistrarAtividadeCommand> validador, IAtividadeRepository atividades,
    IAgendaRepository agenda, ICursoQueries cursos, IUsuarioQueries usuarios, IUsuarioAtual usuario, IRelogio relogio,
    IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache, ServicoFechamento fechamento, ServicoEscala escala)
{
    public async Task<Guid> Handle(RegistrarAtividadeCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        var id = comando.CompromissoId is { } compromissoId
            ? await RegistrarDeCompromissoAsync(compromissoId, comando, ct)
            : await RegistrarAvulsaAsync(comando, ct);
        await cache.InvalidarAreaAsync(ct);
        return id;
    }

    private async Task ValidarCursoAsync(Guid? cursoId, CancellationToken ct)
    {
        if (cursoId is { } curso && !await cursos.CursoAtivoExisteAsync(curso, ct))
            throw Validacao.ErroDeCampo("cursoId", "Curso inexistente ou inativo.");
    }

    private async Task<Guid> RegistrarDeCompromissoAsync(Guid compromissoId, RegistrarAtividadeCommand comando, CancellationToken ct)
    {
        await ValidarCursoAsync(comando.CursoId, ct);
        var intervalo = IntervaloHorario.Criar(comando.Inicio, comando.Fim);
        return await uow.EmTransacaoAsync(async tct =>
        {
            var compromisso = await agenda.ObterAsync(compromissoId, tct) ?? throw new NotFoundException("Compromisso");
            if (!usuario.EhCoordenacao() && compromisso.ProfissionalId != usuario.Id) throw new ForbiddenException();
            await fechamento.ExigirSemanaAbertaAsync(compromisso.ProfissionalId, compromisso.Intervalo, tct);
            await fechamento.ExigirSemanaAbertaAsync(compromisso.ProfissionalId, intervalo, tct);
            if (await atividades.ExisteParaCompromissoAsync(compromissoId, tct))
                throw new ConflictException("Este agendamento já possui atividade registrada.");

            // O intervalo pode ser ajustado: o próprio compromisso (ainda Agendado) não conta como conflito.
            if (intervalo != compromisso.Intervalo)
                await agenda.GarantirSemConflitoAsync(compromisso.ProfissionalId, null, intervalo, compromissoId, true, tct);
            // Mesma regra do agendamento: um início novo passa pela escala (fora dela, só com autorização).
            if (intervalo.Inicio != compromisso.Intervalo.Inicio)
                await escala.ExigirDentroDaEscalaAsync(compromisso.ProfissionalId, intervalo.Inicio, tct);
            var atividade = AtividadeCurso.RegistrarDeCompromisso(compromisso, intervalo, comando.CursoId, comando.Tipo,
                comando.Descricao, usuario.Id, relogio.Agora);
            atividades.Adicionar(atividade);
            auditoria.Registrar("atividade.registro", "AtividadeCurso", atividade.Id);
            await uow.SalvarAsync(tct);
            return atividade.Id;
        }, ct);
    }

    // Avulsa segue a escala como a agendada e o atendimento avulso: fora dela (noturna, feriado), só com autorização.
    private async Task<Guid> RegistrarAvulsaAsync(RegistrarAtividadeCommand comando, CancellationToken ct)
    {
        var profissionalId = await usuarios.ResolverProfissionalAsync(usuario, comando.ProfissionalId, ct);
        await ValidarCursoAsync(comando.CursoId, ct);

        var intervalo = IntervaloHorario.Criar(comando.Inicio, comando.Fim);
        var atividade = AtividadeCurso.RegistrarAvulsa(profissionalId, intervalo, comando.CursoId, comando.Tipo,
            comando.Descricao, comando.JustificativaAvulso!, usuario.Id, relogio.Agora);

        return await uow.EmTransacaoAsync(async tct =>
        {
            await fechamento.ExigirSemanaAbertaAsync(profissionalId, intervalo, tct);
            await agenda.GarantirSemConflitoAsync(profissionalId, null, intervalo, null, true, tct);
            await escala.ExigirDentroDaEscalaAsync(profissionalId, intervalo.Inicio, tct);
            atividades.Adicionar(atividade);
            auditoria.Registrar("atividade.registro", "AtividadeCurso", atividade.Id);
            await uow.SalvarAsync(tct);
            return atividade.Id;
        }, ct);
    }
}
