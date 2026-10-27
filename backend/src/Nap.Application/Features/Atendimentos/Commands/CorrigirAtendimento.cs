using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Producao;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum.ValueObjects;

namespace Nap.Application.Features.Atendimentos.Commands;

public record CorrigirAtendimentoCommand(Guid Id, EstadoAtendimento Estado, TipoAtendimento Tipo, DateTimeOffset Inicio,
    DateTimeOffset Fim, string? DemandaPrincipal, string? ObservacoesPrivadas, string? RecomendacoesEncaminhamento,
    string? ProximosPassos, string? Justificativa, string Versao);

public class CorrigirAtendimentoValidator : AbstractValidator<CorrigirAtendimentoCommand>
{
    public CorrigirAtendimentoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Estado).IsInEnum();
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.Fim).IntervaloValido(x => x.Inicio);
        RuleFor(x => x.DemandaPrincipal).MaximumLength(5000);
        RuleFor(x => x.ObservacoesPrivadas).MaximumLength(5000);
        RuleFor(x => x.RecomendacoesEncaminhamento).MaximumLength(5000);
        RuleFor(x => x.ProximosPassos).MaximumLength(5000);
        RuleFor(x => x.Justificativa).Length(5, 500).When(x => !string.IsNullOrWhiteSpace(x.Justificativa));
    }
}

public sealed class CorrigirAtendimentoHandler(IValidator<CorrigirAtendimentoCommand> validador, IAtendimentoRepository atendimentos,
    IAgendaRepository agenda, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache,
    ServicoEscala escala, ServicoFechamento fechamento)
{
    public async Task Handle(CorrigirAtendimentoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        var atendimento = await atendimentos.ObterAsync(comando.Id, ct) ?? throw new NotFoundException("Atendimento");
        // Responsável (inclusive coordenação corrigindo o próprio atendimento) não precisa de justificativa.
        var ehResponsavel = atendimento.ProfissionalId == usuario.Id;
        if (!ehResponsavel && !usuario.EhCoordenacao()) throw new ForbiddenException();
        uow.ExigirVersao(atendimento, comando.Versao);
        if (!ehResponsavel && string.IsNullOrWhiteSpace(comando.Justificativa))
            throw Validacao.ErroDeCampo("justificativa", "Informe a justificativa da correção.");

        var dados = new DadosClinicos(comando.DemandaPrincipal, comando.ObservacoesPrivadas,
            comando.RecomendacoesEncaminhamento, comando.ProximosPassos);
        var intervalo = IntervaloHorario.Criar(comando.Inicio, comando.Fim);
        // O atendimento é a fonte do tempo ocupado: checa conflito quando o horário muda ou quando ele volta a ocupar
        // horário (Remarcado/Cancelado → Realizado/Ausente) mesmo sem mudar o horário, pois o slot pode ter sido reusado.
        var ocupava = atendimento.Estado is EstadoAtendimento.Realizado or EstadoAtendimento.Ausente;
        var vaiOcupar = comando.Estado is EstadoAtendimento.Realizado or EstadoAtendimento.Ausente;
        var mudouHorario = intervalo != atendimento.Intervalo;
        await uow.EmTransacaoAsync(async tct =>
        {
            await fechamento.ExigirSemanaAbertaAsync(atendimento.ProfissionalId, atendimento.Intervalo, tct);
            await fechamento.ExigirSemanaAbertaAsync(atendimento.ProfissionalId, intervalo, tct);
            if (mudouHorario || (vaiOcupar && !ocupava))
                await agenda.GarantirSemConflitoAsync(atendimento.ProfissionalId, atendimento.AlunoId, intervalo, atendimento.Id, vaiOcupar, tct);
            // Só um início novo passa pela escala: corrigir o texto ou só o término de um atendimento numa data que virou
            // feriado (ou cuja autorização foi revogada) continua possível.
            if (intervalo.Inicio != atendimento.Intervalo.Inicio)
                await escala.ExigirDentroDaEscalaAsync(atendimento.ProfissionalId, comando.Inicio, tct);
            atendimento.Corrigir(comando.Estado, comando.Tipo, intervalo, dados, comando.Justificativa, usuario.Id,
                ehResponsavel, relogio.Agora);
            auditoria.Registrar("atendimento.correcao", "Atendimento", atendimento.Id);
            await uow.SalvarAsync(tct);
            return atendimento.Id;
        }, ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
