using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Cursos;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comum;
using Nap.Domain.Comunicacoes;
using Nap.Domain.Encaminhamentos;

namespace Nap.Application.Features.Comunicacoes;

/// <summary>Vínculos já validados, como serão gravados. <c>EncaminhamentoSip</c>: destino SIP ou Clínica Escola.</summary>
public record VinculosMensagem(Guid? CursoId, Guid? AlunoId, Guid? EncaminhamentoId, bool EncaminhamentoSip)
{
    public bool SemVinculo => CursoId is null && AlunoId is null && EncaminhamentoId is null;
}

public static class RegrasComunicacao
{
    public const string DestinatarioInvalido = "O destinatário não pode receber esta comunicação.";

    /// <summary>Comunicações não existem para a Gestão; perfil desconhecido também é negado.</summary>
    public static void ExigirAcesso(IUsuarioAtual u)
    {
        if (u.Perfil == Perfis.Gestao || !Perfis.Todos.Contains(u.Perfil)) throw new ForbiddenException();
    }

    /// <summary>Rascunho existe só para o autor; enviada, para os dois participantes. Ler e responder também exigem
    /// <see cref="AlcancaVinculos"/>.</summary>
    public static bool PodeVer(IUsuarioAtual u, Mensagem m) =>
        m.Estado == EstadoMensagem.Rascunho ? m.AutorId == u.Id : m.EhParticipante(u.Id);

    /// <summary>Participante que, com o perfil e o curso de hoje, ainda alcança os vínculos: a mesma regra da leitura
    /// (<c>ComunicacaoQueries.Legiveis</c>), para os comandos sobre a mensagem.</summary>
    public static async Task<bool> PodeVerHojeAsync(IUsuarioAtual u, Mensagem m, IAlunoRepository alunos,
        IEncaminhamentoRepository encaminhamentos, CancellationToken ct) =>
        PodeVer(u, m) && (m.Estado == EstadoMensagem.Rascunho
            || AlcancaVinculos(u.Perfil, u.CursoId, await VinculosDaOriginalAsync(m, alunos, encaminhamentos, ct)));

    /// <summary>
    /// Valida os vínculos (no rascunho e no envio) e devolve os que serão gravados. Só com encaminhamento, o aluno é o dele; com aluno, o curso
    /// é o atual do aluno. O aluno é visível à Equipe NAP, ao coordenador do curso atual dele ou por um encaminhamento visível.
    /// </summary>
    public static async Task<VinculosMensagem> ValidarVinculosAsync(IUsuarioAtual u, Guid? cursoId, Guid? alunoId,
        Guid? encaminhamentoId, IAlunoRepository alunos, IEncaminhamentoRepository encaminhamentos, ICursoQueries cursos,
        CancellationToken ct)
    {
        Encaminhamento? encaminhamento = null;
        if (encaminhamentoId is { } idEncaminhamento)
        {
            encaminhamento = await encaminhamentos.ObterAsync(idEncaminhamento, ct);
            if (encaminhamento is null || !AutorizacaoEncaminhamento.PodeVer(u, encaminhamento))
                throw Validacao.ErroDeCampo("encaminhamentoId", "Encaminhamento inexistente.");
            if (alunoId is not null && alunoId != encaminhamento.AlunoId)
                throw Validacao.ErroDeCampo("encaminhamentoId", "O encaminhamento não é deste aluno.");
            alunoId = encaminhamento.AlunoId;
        }

        if (alunoId is { } idAluno)
        {
            var aluno = await alunos.ObterAsync(idAluno, ct);
            // O encaminhamento só abre o aluno ao SIP: o coordenador antigo ainda vê o encaminhamento, mas não o aluno transferido.
            var visivel = aluno is not null && (u.EhEquipeNap() || (u.Perfil == Perfis.Sip && encaminhamento is not null)
                || (u.Perfil == Perfis.CoordenadorCurso && u.CursoId == aluno.CursoAtualId));
            if (!visivel) throw Validacao.ErroDeCampo("alunoId", "Aluno inexistente.");
            if (cursoId is not null && cursoId != aluno!.CursoAtualId)
                throw Validacao.ErroDeCampo("cursoId", "O curso informado não é o curso atual do aluno.");
            cursoId = aluno!.CursoAtualId;
        }
        else if (cursoId is { } idCurso)
        {
            if (!await cursos.CursoAtivoExisteAsync(idCurso, ct))
                throw Validacao.ErroDeCampo("cursoId", "Curso inexistente.");
        }

        var vinculos = new VinculosMensagem(cursoId, alunoId, encaminhamentoId,
            encaminhamento?.Destino is DestinoEncaminhamento.Sip or DestinoEncaminhamento.ClinicaEscola);
        // Quem envia precisa alcançar os vínculos como quem recebe: senão o SIP enviaria, só com curso, uma mensagem que ele mesmo não lê.
        if (!AlcancaVinculos(u.Perfil, u.CursoId, vinculos))
            throw Validacao.ErroDeCampo("cursoId", "Seu perfil ou curso não alcança este vínculo.");
        return vinculos;
    }

    /// <summary>
    /// Vínculos gravados numa mensagem, com o curso atual do aluno: base para revalidar o destinatário de uma resposta (uma
    /// transferência do aluno tira o coordenador antigo da conversa).
    /// </summary>
    public static async Task<VinculosMensagem> VinculosDaOriginalAsync(Mensagem original, IAlunoRepository alunos,
        IEncaminhamentoRepository encaminhamentos, CancellationToken ct)
    {
        var cursoId = original.CursoId;
        if (original.AlunoId is { } alunoId && await alunos.ObterAsync(alunoId, ct) is { } aluno)
            cursoId = aluno.CursoAtualId;
        var sip = original.EncaminhamentoId is { } encaminhamentoId
            && await encaminhamentos.ObterAsync(encaminhamentoId, ct) is
                { Destino: DestinoEncaminhamento.Sip or DestinoEncaminhamento.ClinicaEscola };
        return new VinculosMensagem(cursoId, original.AlunoId, original.EncaminhamentoId, sip);
    }

    /// <summary>No rascunho, o destinatário (se houver) só precisa existir e estar ativo; quem pode receber é conferido no envio.</summary>
    public static async Task ValidarDestinatarioDoRascunhoAsync(IUsuarioAtual u, Guid? destinatarioId, IUsuarioQueries usuarios,
        CancellationToken ct)
    {
        if (destinatarioId is not { } id) return;
        var destinatario = await usuarios.ObterResumoAsync(id, ct);
        if (destinatario is null || !destinatario.Ativo || destinatario.Id == u.Id)
            throw Validacao.ErroDeCampo("destinatarioId", DestinatarioInvalido);
    }

    public static async Task ValidarDestinatarioAsync(IUsuarioAtual u, Guid destinatarioId, VinculosMensagem v,
        IUsuarioQueries usuarios, CancellationToken ct)
    {
        var destinatario = await usuarios.ObterResumoAsync(destinatarioId, ct);
        if (destinatario is null || !PodeReceber(u.Id, destinatario, v))
            throw Validacao.ErroDeCampo("destinatarioId", DestinatarioInvalido);
    }

    /// <summary>Regra única do destinatário: usada no envio e na lista de destinatários disponíveis.</summary>
    public static bool PodeReceber(Guid remetenteId, UsuarioResumoDto d, VinculosMensagem v) =>
        d.Ativo && d.Id != remetenteId && AlcancaVinculos(d.Perfil, d.CursoId, v);

    /// <summary>
    /// Perfil e curso que alcançam os vínculos (com o curso atual do aluno): quem pode receber e, com o perfil de hoje,
    /// quem ainda lê e responde. Espelhada na consulta (<c>ComunicacaoQueries</c>).
    /// </summary>
    public static bool AlcancaVinculos(string perfil, Guid? cursoId, VinculosMensagem v) =>
        perfil != Perfis.Gestao && (v.SemVinculo
            || Perfis.EquipeNap.Contains(perfil)
            || (perfil == Perfis.CoordenadorCurso && cursoId is not null && cursoId == v.CursoId)
            || (perfil == Perfis.Sip && v.EncaminhamentoSip));

    /// <summary>
    /// Lida e arquivamento de cada participante ficam em colunas próprias, mas a versão da linha é uma só: se o outro gravou
    /// entre a leitura e o SaveChanges, relê e aplica de novo uma vez, em vez de devolver um 409 sem sobreposição real.
    /// </summary>
    public static async Task SalvarRelendoSeConflitarAsync(IMensagemRepository mensagens, IUnidadeDeTrabalho uow, Mensagem m,
        Action aplicar, CancellationToken ct)
    {
        aplicar();
        try
        {
            await uow.SalvarAsync(ct);
        }
        catch (ConflictException)
        {
            if (!await mensagens.RecarregarAsync(m, ct)) throw new NotFoundException("Mensagem");
            aplicar();
            await uow.SalvarAsync(ct);
        }
    }
}
