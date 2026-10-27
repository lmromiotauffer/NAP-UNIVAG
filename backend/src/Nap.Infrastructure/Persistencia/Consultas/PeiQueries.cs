using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Peis;
using Nap.Domain.Peis;

namespace Nap.Infrastructure.Persistencia.Consultas;

// ForaDaVigencia segue false aqui: os handlers preenchem com o hoje civil do fuso institucional.
public sealed class PeiQueries(NapDbContext db) : IPeiQueries
{
    public async Task<IReadOnlyList<PeiResumoDto>?> ListarDoAlunoAsync(Guid alunoId, EscopoPeis e, Guid usuarioId, CancellationToken ct)
    {
        // Mesmo recorte da ficha: coordenador de curso alcança o aluno que passou pelo curso dele (curso da época).
        var curso = e.SomenteCursoId;
        var aluno = db.Alunos.AsNoTracking().Where(a => a.Id == alunoId);
        if (!await aluno.AnyAsync(ct)) return null;
        var alunoNoCurso = curso is not { } c || await aluno.Where(AlunoQueries.PassouPeloCurso(c)).AnyAsync(ct);

        var linhas = await Visiveis(db, db.Peis.AsNoTracking().Where(p => p.AlunoId == alunoId).SelectMany(p => p.Versoes), e)
            .Select(v => new
            {
                v.PeiId, v.Id, v.Numero, v.Estado, Inicio = v.Conteudo.Vigencia.Inicio, Fim = v.Conteudo.Vigencia.Fim, v.PublicadaEm,
                Ciente = v.Ciencias.Any(c => c.CoordenadorId == usuarioId),
                PeiCriadoEm = db.Peis.Where(p => p.Id == v.PeiId).Select(p => p.CriadoEm).First(),
                Responsavel = db.Users.Where(u => db.Peis.Any(p => p.Id == v.PeiId && p.ProfissionalResponsavelId == u.Id))
                    .Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
            })
            .ToListAsync(ct);

        // O coordenador de curso só enxerga o aluno que passou pelo próprio curso ou aquele de quem já publicou PEI nele; fora disso, 404.
        if (alunoNoCurso == false && linhas.Count == 0) return null;

        return linhas
            .GroupBy(l => new { l.PeiId, l.Responsavel, l.PeiCriadoEm })
            .OrderBy(g => g.Key.PeiCriadoEm).ThenBy(g => g.Key.PeiId)
            .Select(g => new PeiResumoDto(g.Key.PeiId, g.Key.Responsavel,
                g.OrderByDescending(l => l.Numero)
                    .Select(l => new VersaoResumoDto(l.Id, l.Numero, l.Estado, l.Inicio, l.Fim, l.PublicadaEm, l.Ciente, false))
                    .ToList()))
            .ToList();
    }

    public async Task<VersaoPeiDetalheDto?> ObterVersaoAsync(Guid versaoId, EscopoPeis e, IUsuarioAtual u, CancellationToken ct)
    {
        // A demanda interna só entra na consulta (e portanto no SQL) para a Equipe NAP.
        var verDemanda = e.Todos;
        var linha = await Visiveis(db, db.Peis.AsNoTracking().SelectMany(p => p.Versoes), e)
            .Where(v => v.Id == versaoId)
            .Select(v => new
            {
                v.Id, v.PeiId, v.Numero, v.Estado, v.HashConteudo, v.PublicadaEm, v.CursoPublicacaoId,
                Demanda = verDemanda ? v.Conteudo.DemandaInterna : null,
                v.Conteudo.NecessidadesEducacionais, v.Conteudo.Adaptacoes, v.Conteudo.OrientacoesProfessores,
                v.Conteudo.RecursosApoio, v.Conteudo.CuidadosPedagogicos,
                Inicio = v.Conteudo.Vigencia.Inicio, Fim = v.Conteudo.Vigencia.Fim,
                PublicadaPor = db.Users.Where(us => us.Id == v.PublicadaPorId).Select(us => us.NomeCompleto).FirstOrDefault(),
                // Publicada: o curso da publicação (o aluno pode ter sido transferido depois); detalhe e PDF mostram esse.
                CursoPublicacao = db.Cursos.Where(c => c.Id == v.CursoPublicacaoId).Select(c => c.Nome).FirstOrDefault(),
                Assinaturas = v.Assinaturas.Where(a => a.InvalidadaEm == null).OrderBy(a => a.Em).ThenBy(a => a.Papel)
                    .Select(a => new { a.Papel, a.NomeSignatario, a.Em, a.Metodo }).ToList(),
                Ciencias = v.Ciencias.OrderBy(c => c.Em).ThenBy(c => c.Id)
                    .Select(c => new
                    {
                        Coordenador = db.Users.Where(us => us.Id == c.CoordenadorId).Select(us => us.NomeCompleto).FirstOrDefault() ?? "",
                        c.Em, Minha = c.CoordenadorId == u.Id,
                    }).ToList(),
                Versao = EF.Property<byte[]>(v, "Versao"),
            })
            .SingleOrDefaultAsync(ct);
        if (linha is null) return null;

        var pei = await db.Peis.AsNoTracking().Where(p => p.Id == linha.PeiId)
            .Select(p => new
            {
                p.AlunoId, p.ProfissionalResponsavelId,
                CursoAtualAlunoId = db.Alunos.Where(a => a.Id == p.AlunoId).Select(a => (Guid?)a.CursoAtualId).FirstOrDefault(),
                Responsavel = db.Users.Where(us => us.Id == p.ProfissionalResponsavelId).Select(us => us.NomeCompleto).FirstOrDefault() ?? "",
                MaisRecente = p.Versoes.Max(x => x.Numero),
                ImpedeNovaVersao = p.Versoes.Any(x => VersaoPei.EstadosQueImpedemNovaVersao.Contains(x.Estado)),
                Aluno = db.Alunos.Where(a => a.Id == p.AlunoId)
                    .Select(a => new
                    {
                        a.NomeCompleto, Matricula = a.Matricula.Valor,
                        Curso = db.Cursos.Where(c => c.Id == a.CursoAtualId).Select(c => c.Nome).FirstOrDefault() ?? "",
                    }).FirstOrDefault(),
            })
            .SingleAsync(ct);

        var temPedagogico = ConteudoPei.PossuiConteudoPedagogico(linha.NecessidadesEducacionais, linha.Adaptacoes,
            linha.OrientacoesProfessores, linha.RecursosApoio, linha.CuidadosPedagogicos);
        var acoes = AutorizacaoPei.Acoes(u, new AutorizacaoPei.SituacaoVersao(pei.ProfissionalResponsavelId, linha.CursoPublicacaoId,
            linha.Estado, temPedagogico,
            linha.Assinaturas.Any(a => a.Papel == PapelAssinatura.Profissional), linha.Assinaturas.Any(a => a.Papel == PapelAssinatura.Aluno),
            Pei.EhMaisRecente(linha.Numero, pei.MaisRecente), pei.ImpedeNovaVersao, linha.Ciencias.Any(c => c.Minha),
            pei.CursoAtualAlunoId));

        return new VersaoPeiDetalheDto(linha.Id, linha.PeiId, pei.AlunoId, pei.Aluno?.NomeCompleto ?? "", pei.Aluno?.Matricula ?? "",
            // Versão já assinada pelo profissional: o responsável é quem assinou, mesmo depois de uma troca de responsável.
            linha.CursoPublicacao ?? pei.Aluno?.Curso ?? "",
            linha.Assinaturas.FirstOrDefault(a => a.Papel == PapelAssinatura.Profissional)?.NomeSignatario ?? pei.Responsavel,
            linha.Numero, linha.Estado,
            new ConteudoPeiDto(linha.Demanda, linha.NecessidadesEducacionais, linha.Adaptacoes, linha.OrientacoesProfessores,
                linha.RecursosApoio, linha.CuidadosPedagogicos, linha.Inicio, linha.Fim),
            linha.HashConteudo,
            linha.Assinaturas.Select(a => new AssinaturaDto(a.Papel, a.NomeSignatario, a.Em, a.Metodo)).ToList(),
            linha.Ciencias.Select(c => new CienciaDto(c.Coordenador, c.Em)).ToList(),
            linha.PublicadaEm, linha.PublicadaPor, acoes, Convert.ToBase64String(linha.Versao), false);
    }

    /// <summary>Recorte por perfil sobre versões: o coordenador de curso só vê as publicadas (inclusive as depois arquivadas)
    /// no curso da publicação e a publicada em vigor do aluno que hoje está no curso dele. Espelha
    /// <see cref="AutorizacaoPei.PodeVer"/>; compartilhado com o painel.</summary>
    internal static IQueryable<VersaoPei> Visiveis(NapDbContext db, IQueryable<VersaoPei> q, EscopoPeis e)
    {
        if (e.Todos) return q;
        if (e.SomenteCursoId is { } curso)
            return q.Where(v => v.PublicadaEm != null && (v.CursoPublicacaoId == curso
                || (v.Estado == EstadoVersaoPei.Publicada
                    && db.Peis.Any(p => p.Id == v.PeiId && db.Alunos.Any(a => a.Id == p.AlunoId && a.CursoAtualId == curso)))));
        return q.Where(_ => false); // escopo sem recorte reconhecido: falha fechada
    }
}
