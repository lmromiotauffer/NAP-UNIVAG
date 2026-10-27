using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Comunicacoes;
using Nap.Domain.Comum;
using Nap.Domain.Comunicacoes;
using Nap.Domain.Encaminhamentos;

namespace Nap.Infrastructure.Persistencia.Consultas;

/// <summary>Recorte por participante (nenhuma consulta lê mensagem de quem não é autor ou destinatário dela) e, fora da Equipe
/// NAP, pelos vínculos que o perfil e o curso de hoje ainda alcançam.</summary>
public sealed class ComunicacaoQueries(NapDbContext db) : IComunicacaoQueries
{
    public async Task<PagedResult<MensagemListaDto>> ListarAsync(IUsuarioAtual leitor, CaixaMensagens caixa, string? busca, Guid? alunoId, Guid? encaminhamentoId,
        int pagina, int tamanho, CancellationToken ct)
    {
        var usuarioId = leitor.Id;
        var q = Legiveis(db.Mensagens.AsNoTracking(), leitor);
        q = caixa switch
        {
            CaixaMensagens.Entrada => q.Where(m => m.DestinatarioId == usuarioId && m.Estado == EstadoMensagem.Enviada
                && m.ArquivadaPeloDestinatarioEm == null),
            CaixaMensagens.Enviados => q.Where(m => m.AutorId == usuarioId && m.Estado == EstadoMensagem.Enviada
                && m.ArquivadaPeloAutorEm == null),
            CaixaMensagens.Rascunhos => q.Where(m => m.AutorId == usuarioId && m.Estado == EstadoMensagem.Rascunho),
            CaixaMensagens.Arquivadas => q.Where(m => m.Estado == EstadoMensagem.Enviada
                && ((m.AutorId == usuarioId && m.ArquivadaPeloAutorEm != null)
                    || (m.DestinatarioId == usuarioId && m.ArquivadaPeloDestinatarioEm != null))),
            _ => q.Where(_ => false),
        };
        if (!string.IsNullOrWhiteSpace(busca))
        {
            // Contains vira LIKE com o parâmetro escapado pelo EF: %, _ e [ do usuário são literais.
            var termo = busca.Trim();
            q = q.Where(m => m.Assunto.Contains(termo) || m.Corpo.Contains(termo));
        }
        if (alunoId is { } aluno) q = q.Where(m => m.AlunoId == aluno);
        if (encaminhamentoId is { } encaminhamento) q = q.Where(m => m.EncaminhamentoId == encaminhamento);

        var total = await q.CountAsync(ct);
        var itens = await q
            .OrderByDescending(m => m.EnviadaEm ?? m.AtualizadaEm).ThenByDescending(m => m.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(m => new MensagemListaDto(m.Id, m.Tipo, m.Assunto,
                db.Users.Where(u => u.Id == m.AutorId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                db.Users.Where(u => u.Id == m.DestinatarioId).Select(u => u.NomeCompleto).FirstOrDefault(),
                m.AlunoId, db.Alunos.Where(a => a.Id == m.AlunoId).Select(a => a.NomeCompleto).FirstOrDefault(),
                m.EnviadaEm ?? m.AtualizadaEm, m.LidaEm != null,
                m.DestinatarioId == usuarioId && m.Estado == EstadoMensagem.Enviada))
            .ToListAsync(ct);
        return new PagedResult<MensagemListaDto>(itens, pagina, tamanho, total);
    }

    public async Task<MensagemDetalheDto?> ObterAsync(Guid id, IUsuarioAtual leitor, CancellationToken ct)
    {
        var usuarioId = leitor.Id;
        // Mesmo recorte de RegrasComunicacao.PodeVer: rascunho só do autor; enviada dos participantes.
        var linha = await Legiveis(db.Mensagens.AsNoTracking(), leitor)
            .Where(m => m.Id == id && (m.Estado == EstadoMensagem.Rascunho
                ? m.AutorId == usuarioId
                : m.AutorId == usuarioId || m.DestinatarioId == usuarioId))
            .Select(m => new
            {
                m.FioId,
                Dto = new MensagemDetalheDto(m.Id, m.Tipo, m.Assunto, m.AutorId,
                    db.Users.Where(u => u.Id == m.AutorId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                    m.DestinatarioId, db.Users.Where(u => u.Id == m.DestinatarioId).Select(u => u.NomeCompleto).FirstOrDefault(),
                    m.AlunoId, db.Alunos.Where(a => a.Id == m.AlunoId).Select(a => a.NomeCompleto).FirstOrDefault(),
                    m.EnviadaEm ?? m.AtualizadaEm, m.LidaEm != null,
                    m.DestinatarioId == usuarioId && m.Estado == EstadoMensagem.Enviada,
                    m.Corpo, m.EncaminhamentoId, m.CursoId, m.Estado,
                    // Melhor esforço: o outro participante ainda ativo e fora da Gestão; os vínculos são conferidos ao responder.
                    m.Estado == EstadoMensagem.Enviada && db.Users.Any(u =>
                        (Guid?)u.Id == (m.AutorId == usuarioId ? m.DestinatarioId : m.AutorId) && u.Ativo && u.Perfil != Perfis.Gestao),
                    m.Estado == EstadoMensagem.Enviada && m.DestinatarioId == usuarioId && m.LidaEm == null,
                    m.AutorId == usuarioId ? m.ArquivadaPeloAutorEm != null : m.ArquivadaPeloDestinatarioEm != null,
                    new List<MensagemFioDto>(),
                    Convert.ToBase64String(EF.Property<byte[]>(m, "Versao"))),
            })
            .SingleOrDefaultAsync(ct);
        if (linha is null) return null;

        var fio = await Legiveis(db.Mensagens.AsNoTracking(), leitor)
            .Where(m => m.FioId == linha.FioId && m.Estado == EstadoMensagem.Enviada
                && (m.AutorId == usuarioId || m.DestinatarioId == usuarioId))
            .OrderBy(m => m.EnviadaEm).ThenBy(m => m.Id)
            .Select(m => new MensagemFioDto(m.Id, m.AutorId,
                db.Users.Where(u => u.Id == m.AutorId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                m.Assunto, m.Corpo, m.EnviadaEm!.Value))
            .ToListAsync(ct);
        return linha.Dto with { Fio = fio };
    }

    public Task<int> ContarNaoLidasAsync(IUsuarioAtual leitor, CancellationToken ct) =>
        Legiveis(db.Mensagens, leitor).CountAsync(m => m.DestinatarioId == leitor.Id && m.Estado == EstadoMensagem.Enviada
            && m.LidaEm == null && m.ArquivadaPeloDestinatarioEm == null, ct);

    /// <summary>
    /// Espelha <see cref="RegrasComunicacao.AlcancaVinculos"/> com o curso atual do aluno: fora da Equipe NAP, a mensagem
    /// enviada só é lida enquanto o perfil e o curso de hoje alcançam os vínculos dela. O rascunho é só do autor.
    /// </summary>
    private IQueryable<Mensagem> Legiveis(IQueryable<Mensagem> q, IUsuarioAtual leitor)
    {
        if (leitor.EhEquipeNap()) return q;
        if (leitor.Perfil == Perfis.CoordenadorCurso && leitor.CursoId is { } curso)
            return q.Where(m => m.Estado == EstadoMensagem.Rascunho
                || (m.CursoId == null && m.AlunoId == null && m.EncaminhamentoId == null)
                || (m.AlunoId != null ? db.Alunos.Any(a => a.Id == m.AlunoId && a.CursoAtualId == curso) : m.CursoId == curso));
        if (leitor.Perfil == Perfis.Sip)
            return q.Where(m => m.Estado == EstadoMensagem.Rascunho
                || (m.CursoId == null && m.AlunoId == null && m.EncaminhamentoId == null)
                || db.Encaminhamentos.Any(e => e.Id == m.EncaminhamentoId
                    && (e.Destino == DestinoEncaminhamento.Sip || e.Destino == DestinoEncaminhamento.ClinicaEscola)));
        return q.Where(_ => false); // perfil sem recorte reconhecido: falha fechada
    }
}
