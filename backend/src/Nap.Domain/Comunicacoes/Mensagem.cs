using Nap.Domain.Comum;

namespace Nap.Domain.Comunicacoes;

public class Mensagem : AgregadoComEventos
{
    private const string PrefixoResposta = "Re: ";

    public Guid AutorId { get; private set; }
    public Guid? DestinatarioId { get; private set; }
    public TipoMensagem Tipo { get; private set; }
    public string Assunto { get; private set; } = null!;
    public string Corpo { get; private set; } = null!;
    public Guid? CursoId { get; private set; }
    public Guid? AlunoId { get; private set; }
    public Guid? EncaminhamentoId { get; private set; }
    public Guid? PeiVersaoId { get; private set; }
    public Guid FioId { get; private set; }
    public Guid? RespostaDeId { get; private set; }
    public EstadoMensagem Estado { get; private set; }
    public DateTimeOffset CriadaEm { get; private set; }
    public DateTimeOffset AtualizadaEm { get; private set; }
    public DateTimeOffset? EnviadaEm { get; private set; }
    public DateTimeOffset? LidaEm { get; private set; }
    public DateTimeOffset? ArquivadaPeloAutorEm { get; private set; }
    public DateTimeOffset? ArquivadaPeloDestinatarioEm { get; private set; }

    private Mensagem() { } // ORM

    public static Mensagem CriarRascunho(Guid autorId, TipoMensagem tipo, string assunto, string corpo,
        Guid? destinatarioId, Guid? cursoId, Guid? alunoId, Guid? encaminhamentoId, DateTimeOffset agora)
    {
        var m = new Mensagem
        {
            AutorId = autorId,
            Estado = EstadoMensagem.Rascunho,
            CriadaEm = agora,
            AtualizadaEm = agora,
        };
        m.FioId = m.Id;
        m.Preencher(tipo, assunto, corpo, destinatarioId, cursoId, alunoId, encaminhamentoId);
        return m;
    }

    /// <summary>Mensagem do sistema ao publicar um PEI: nasce já enviada; o envio manual desse tipo é rejeitado na Application.</summary>
    public static Mensagem CriarEnvioPei(Guid autorId, Guid destinatarioId, string assunto, string corpo, Guid cursoId,
        Guid alunoId, Guid peiVersaoId, DateTimeOffset agora)
    {
        var m = new Mensagem
        {
            AutorId = autorId,
            PeiVersaoId = peiVersaoId,
            Estado = EstadoMensagem.Rascunho,
            CriadaEm = agora,
            AtualizadaEm = agora,
        };
        m.FioId = m.Id;
        m.Preencher(TipoMensagem.EnvioPei, assunto, corpo, destinatarioId, cursoId, alunoId, null);
        m.Enviar(agora);
        return m;
    }

    /// <param name="cursoAtualId">Curso atual do aluno no envio da resposta; sem ele, copia o curso da original.</param>
    public static Mensagem CriarResposta(Mensagem original, Guid autorId, string corpo, DateTimeOffset agora,
        Guid? cursoAtualId = null)
    {
        if (original.Estado != EstadoMensagem.Enviada)
            throw new DomainException("Só é possível responder a uma mensagem enviada.");
        if (!original.EhParticipante(autorId))
            throw new DomainException("Apenas participantes da conversa podem responder.");

        var assunto = original.Assunto.StartsWith(PrefixoResposta, StringComparison.OrdinalIgnoreCase)
            ? original.Assunto
            : PrefixoResposta + original.Assunto;
        assunto = Texto.Truncar(assunto, 200);

        var destinatario = autorId == original.AutorId ? original.DestinatarioId : original.AutorId;
        var m = new Mensagem
        {
            AutorId = autorId,
            FioId = original.FioId,
            RespostaDeId = original.Id,
            PeiVersaoId = original.PeiVersaoId,
            Estado = EstadoMensagem.Rascunho,
            CriadaEm = agora,
            AtualizadaEm = agora,
        };
        m.Preencher(original.Tipo, assunto, corpo, destinatario, cursoAtualId ?? original.CursoId, original.AlunoId,
            original.EncaminhamentoId);
        m.Enviar(agora);
        return m;
    }

    public void AtualizarRascunho(TipoMensagem tipo, string assunto, string corpo, Guid? destinatarioId,
        Guid? cursoId, Guid? alunoId, Guid? encaminhamentoId, DateTimeOffset agora)
    {
        ExigirRascunho();
        Preencher(tipo, assunto, corpo, destinatarioId, cursoId, alunoId, encaminhamentoId);
        AtualizadaEm = agora;
    }

    public void Enviar(DateTimeOffset agora)
    {
        ExigirRascunho();
        if (DestinatarioId is not { } destinatario)
            throw new DomainException("O destinatário é obrigatório para enviar a mensagem.");
        Estado = EstadoMensagem.Enviada;
        EnviadaEm = agora;
        AtualizadaEm = agora;
        Registrar(new MensagemEnviada(agora, Id, AutorId, destinatario, Tipo));
    }

    public void MarcarLida(Guid usuarioId, DateTimeOffset agora)
    {
        if (Estado != EstadoMensagem.Enviada || usuarioId != DestinatarioId)
            throw new DomainException("Somente o destinatário pode marcar a mensagem como lida.");
        if (LidaEm is not null) return;
        LidaEm = agora;
    }

    public void Arquivar(Guid usuarioId, DateTimeOffset agora)
    {
        ExigirParticipanteEnviada(usuarioId);
        if (usuarioId == AutorId) ArquivadaPeloAutorEm ??= agora;
        else ArquivadaPeloDestinatarioEm ??= agora;
    }

    public void Desarquivar(Guid usuarioId)
    {
        ExigirParticipanteEnviada(usuarioId);
        if (usuarioId == AutorId) ArquivadaPeloAutorEm = null;
        else ArquivadaPeloDestinatarioEm = null;
    }

    public bool EhParticipante(Guid usuarioId) => usuarioId == AutorId || usuarioId == DestinatarioId;

    public void ExigirRascunho()
    {
        if (Estado != EstadoMensagem.Rascunho)
            throw new DomainException("A mensagem já foi enviada e não pode mais ser alterada.");
    }

    private void ExigirParticipanteEnviada(Guid usuarioId)
    {
        if (Estado != EstadoMensagem.Enviada)
            throw new DomainException("Só é possível arquivar mensagens enviadas.");
        if (!EhParticipante(usuarioId))
            throw new DomainException("Apenas participantes da conversa podem arquivar a mensagem.");
    }

    private void Preencher(TipoMensagem tipo, string assunto, string corpo, Guid? destinatarioId, Guid? cursoId,
        Guid? alunoId, Guid? encaminhamentoId)
    {
        var assuntoValido = Texto.Validar(assunto, 3, 200, "O assunto");
        var corpoValido = Texto.Validar(corpo, 1, 10000, "O corpo");
        if (destinatarioId == AutorId)
            throw new DomainException("O destinatário deve ser diferente do remetente.");
        Tipo = tipo;
        Assunto = assuntoValido;
        Corpo = corpoValido;
        DestinatarioId = destinatarioId;
        CursoId = cursoId;
        AlunoId = alunoId;
        EncaminhamentoId = encaminhamentoId;
    }
}
