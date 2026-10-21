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

    /// <summary>Rascunho existe só para o autor; enviada, para os dois participantes.</summary>
    public static bool PodeVer(IUsuarioAtual u, Mensagem m) =>
        m.Estado == EstadoMensagem.Rascunho ? m.AutorId == u.Id : m.EhParticipante(u.Id);

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
            var visivel = aluno is not null && (u.EhEquipeNap() || encaminhamento is not null
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
            if (u.Perfil == Perfis.CoordenadorCurso && u.CursoId != idCurso)
                throw Validacao.ErroDeCampo("cursoId", "Use o seu próprio curso.");
        }

        return new VinculosMensagem(cursoId, alunoId, encaminhamentoId,
            encaminhamento?.Destino is DestinoEncaminhamento.Sip or DestinoEncaminhamento.ClinicaEscola);
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
        d.Ativo && d.Id != remetenteId && d.Perfil != Perfis.Gestao && (v.SemVinculo
            || Perfis.EquipeNap.Contains(d.Perfil)
            || (d.Perfil == Perfis.CoordenadorCurso && d.CursoId is not null && d.CursoId == v.CursoId)
            || (d.Perfil == Perfis.Sip && v.EncaminhamentoSip));
}
