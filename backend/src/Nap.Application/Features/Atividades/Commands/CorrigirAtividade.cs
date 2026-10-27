using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Cursos;
using Nap.Application.Features.Producao;
using Nap.Domain.Atividades;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Features.Atividades.Commands;

public record CorrigirAtividadeCommand(Guid Id, string Versao, DateTimeOffset Inicio, DateTimeOffset Fim, Guid? CursoId,
    TipoAtividade Tipo, string Descricao, string Justificativa);

public class CorrigirAtividadeValidator : AbstractValidator<CorrigirAtividadeCommand>
{
    public CorrigirAtividadeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.Descricao).NotEmpty().WithMessage("Informe a descrição da atividade.")
            .Must(d => d is null || d.Trim().Length is >= 3 and <= 2000).WithMessage("A descrição deve ter de 3 a 2000 caracteres.");
        RuleFor(x => x.Fim).IntervaloValido(x => x.Inicio);
        RuleFor(x => x.Justificativa).NotEmpty().WithMessage("Informe a justificativa da correção.")
            .Must(j => j is null || j.Trim().Length is >= 5 and <= 500).WithMessage("A justificativa deve ter de 5 a 500 caracteres.");
    }
}

public sealed class CorrigirAtividadeHandler(IValidator<CorrigirAtividadeCommand> validador, IAtividadeRepository atividades,
    IAtividadeQueries consultas, IAgendaRepository agenda, ICursoQueries cursos, IUsuarioAtual usuario, IRelogio relogio,
    IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache, ServicoFechamento fechamento, ServicoEscala escala)
{
    public async Task<AtividadeDetalheDto> Handle(CorrigirAtividadeCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        var atividade = await atividades.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Atividade");
        if (atividade.ProfissionalId != usuario.Id && !usuario.EhCoordenacao()) throw new ForbiddenException();
        uow.ExigirVersao(atividade, comando.Versao);

        // Manter o curso atual (mesmo inativo) é permitido; trocar exige curso ativo.
        if (comando.CursoId is { } curso && curso != atividade.CursoId && !await cursos.CursoAtivoExisteAsync(curso, ct))
            throw Validacao.ErroDeCampo("cursoId", "Curso inexistente ou inativo.");

        var intervalo = IntervaloHorario.Criar(comando.Inicio, comando.Fim);
        await uow.EmTransacaoAsync(async tct =>
        {
            await fechamento.ExigirSemanaAbertaAsync(atividade.ProfissionalId, atividade.Intervalo, tct);
            await fechamento.ExigirSemanaAbertaAsync(atividade.ProfissionalId, intervalo, tct);
            if (intervalo != atividade.Intervalo)
                await agenda.GarantirSemConflitoAsync(atividade.ProfissionalId, null, intervalo, atividade.Id, true, tct);
            // Como no atendimento: só um início novo passa pela escala.
            if (intervalo.Inicio != atividade.Intervalo.Inicio)
                await escala.ExigirDentroDaEscalaAsync(atividade.ProfissionalId, intervalo.Inicio, tct);
            atividade.Corrigir(intervalo, comando.CursoId, comando.Tipo, comando.Descricao, comando.Justificativa, usuario.Id,
                relogio.Agora);
            auditoria.Registrar("atividade.correcao", "AtividadeCurso", atividade.Id);
            await uow.SalvarAsync(tct);
            return atividade.Id;
        }, ct);
        await cache.InvalidarAreaAsync(ct);

        var detalhe = await consultas.ObterAsync(atividade.Id, null, ct) ?? throw new NotFoundException("Atividade");
        return detalhe with { PodeCorrigir = true };
    }
}
