using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Comunicacoes;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comum;
using Nap.Domain.Comunicacoes;
using Nap.Domain.Encaminhamentos;
using Nap.Domain.Notificacoes;

namespace Nap.Application.Features.Notificacoes.Commands;

/// <summary>
/// Consumidor dos eventos da outbox. Roda sem usuário autenticado: não usa IUsuarioAtual nem IAuditoria (criar notificação
/// não é ação auditada). Idempotente: reprocessar o mesmo evento só cria as notificações que faltam.
/// </summary>
public sealed class GerarNotificacoesHandler(INotificacaoRepository notificacoes, IUsuarioQueries usuarios, IAlunoRepository alunos,
    IMensagemRepository mensagens, IEnvioEmail email, IRelogio relogio, IUnidadeDeTrabalho uow)
{
    public const string AssuntoEmail = "Você tem uma nova notificação no NAP";
    private const string CorpoEmail = "Acesse o NAP para ver os detalhes.";

    /// <summary>Rota da SPA da mensagem; a leitura dela (MarcarMensagemLida) acha a notificação por este link.</summary>
    public static string LinkMensagem(Guid mensagemId) => $"/comunicacoes/{mensagemId}";

    /// <returns>Quantidade de notificações criadas nesta execução.</returns>
    public async Task<int> Handle(IEventoDominio evento, CancellationToken ct = default)
    {
        if (evento is not (EncaminhamentoCriado or RetornoEncaminhamentoRegistrado or MensagemEnviada))
            throw new InvalidOperationException($"Evento sem consumidor de notificações: {evento.GetType().Name}");
        var destinatarios = await DestinatariosAsync(evento, ct);
        var jaNotificados = await notificacoes.DestinatariosJaNotificadosAsync(evento.EventoId, ct);
        var faltantes = destinatarios.Except(jaNotificados).ToList();
        if (faltantes.Count == 0) return 0;

        var (titulo, texto, link) = await ConteudoAsync(evento, ct);
        // Nome de aluno e assunto vão até 200 caracteres: o título é cortado para caber, senão o evento falharia para sempre.
        titulo = Texto.Truncar(titulo, 200, "…");
        var novas = faltantes.Select(d => Notificacao.Criar(d, evento.EventoId, evento.GetType().Name, titulo, texto,
            link, relogio.Agora)).ToList();
        foreach (var n in novas)
            notificacoes.Adicionar(n);
        // A notificação interna é gravada antes do e-mail: uma falha no envio nunca a desfaz.
        await uow.SalvarAsync(ct);

        foreach (var n in novas)
        {
            try
            {
                n.DefinirEstadoEmail(await email.EnviarAsync(n.DestinatarioId, AssuntoEmail, CorpoEmail, ct));
            }
            // O log da falha é da implementação de IEnvioEmail (Infrastructure).
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                n.DefinirEstadoEmail(EstadoEmail.Falhou);
            }
        }
        await uow.SalvarAsync(ct);
        return novas.Count;
    }

    private async Task<(string Titulo, string Texto, string Link)> ConteudoAsync(IEventoDominio evento, CancellationToken ct)
    {
        switch (evento)
        {
            case EncaminhamentoCriado c:
                var alunoCriado = await NomeDoAlunoAsync(c.AlunoId, ct);
                return ($"Novo encaminhamento: {alunoCriado}", $"{Rotulos.De(c.Especialidade)} · {Rotulos.De(c.Destino)}",
                    $"/encaminhamentos/{c.EncaminhamentoId}");
            case RetornoEncaminhamentoRegistrado r:
                var alunoRetorno = await NomeDoAlunoAsync(r.AlunoId, ct);
                return ($"Retorno do encaminhamento de {alunoRetorno}", $"{Rotulos.De(r.Acao)}: {Rotulos.De(r.Estado)}",
                    $"/encaminhamentos/{r.EncaminhamentoId}");
            case MensagemEnviada m:
                var mensagem = await mensagens.ObterAsync(m.MensagemId, ct) ?? throw new NotFoundException("Mensagem");
                var remetente = (await usuarios.ObterResumoAsync(m.RemetenteId, ct))?.Nome;
                var tipo = Rotulos.De(m.Tipo);
                return ($"Nova mensagem: {mensagem.Assunto}", remetente is null ? tipo : $"{remetente} · {tipo}",
                    LinkMensagem(m.MensagemId));
            default:
                throw new InvalidOperationException();
        }
    }

    private async Task<string> NomeDoAlunoAsync(Guid alunoId, CancellationToken ct) =>
        (await alunos.ObterAsync(alunoId, ct) ?? throw new NotFoundException("Aluno")).NomeCompleto;

    // Sem destinatário ativo, ninguém mais é notificado: vira alerta no painel da coordenação (calculado na consulta).
    private async Task<IReadOnlyList<Guid>> DestinatariosAsync(IEventoDominio evento, CancellationToken ct) => evento switch
    {
        EncaminhamentoCriado { Destino: DestinoEncaminhamento.Sip or DestinoEncaminhamento.ClinicaEscola } =>
            await usuarios.ListarAtivosPorPerfilAsync(Perfis.Sip, null, ct),
        EncaminhamentoCriado { Destino: DestinoEncaminhamento.CoordenacaoCurso } c =>
            await usuarios.ListarAtivosPorPerfilAsync(Perfis.CoordenadorCurso, c.CursoId, ct),
        RetornoEncaminhamentoRegistrado r when r.SolicitanteId != r.AutorId =>
            await usuarios.EstaAtivoAsync(r.SolicitanteId, ct) ? [r.SolicitanteId] : [],
        // Lida antes de a outbox rodar: a notificação "Nova mensagem" nasceria não lida para algo já visto.
        MensagemEnviada m => await mensagens.ObterAsync(m.MensagemId, ct) is { LidaEm: null }
            && await usuarios.EstaAtivoAsync(m.DestinatarioId, ct) ? [m.DestinatarioId] : [],
        _ => [],
    };
}
