using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Documentos;
using Nap.Domain.Documentos;

namespace Nap.Infrastructure.Persistencia.Consultas;

public sealed class DocumentoQueries(NapDbContext db) : IDocumentoQueries
{
    public async Task<PagedResult<DocumentoDto>?> ListarAsync(Guid alunoId, EscopoDocumentos e, int pagina, int tamanho,
        CancellationToken ct)
    {
        // Mesmo recorte de alunos: o coordenador só alcança a ficha de aluno que passou pelo curso dele.
        var curso = e.SomenteCursoId;
        var aluno = db.Alunos.Where(a => a.Id == alunoId);
        if (!await (curso is { } c ? aluno.Where(AlunoQueries.PassouPeloCurso(c)) : aluno).AnyAsync(ct)) return null;

        var q = NoEscopo(e).Where(d => d.AlunoId == alunoId);
        var total = await q.CountAsync(ct);
        var itens = await Projetar(q.OrderByDescending(d => d.CriadoEm).ThenByDescending(d => d.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho)).ToListAsync(ct);
        return new PagedResult<DocumentoDto>(itens, pagina, tamanho, total);
    }

    public Task<DocumentoDto?> ObterAsync(Guid id, EscopoDocumentos e, CancellationToken ct) =>
        Projetar(NoEscopo(e).Where(d => d.Id == id)).SingleOrDefaultAsync(ct);

    public Task<ArquivoDocumento?> ObterArquivoAsync(Guid id, EscopoDocumentos e, CancellationToken ct) =>
        NoEscopo(e).Where(d => d.Id == id)
            .Select(d => new ArquivoDocumento(d.Id, d.ChaveArmazenamento, d.NomeArquivo, d.TipoConteudo))
            .SingleOrDefaultAsync(ct);

    private IQueryable<DocumentoDto> Projetar(IQueryable<Documento> q) => q.Select(d => new DocumentoDto(d.Id, d.Categoria,
        d.Descricao, d.NomeArquivo, d.TamanhoBytes,
        db.Users.Where(u => u.Id == d.CriadoPorId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
        d.CriadoEm, d.Visibilidade,
        d.Visibilidade == VisibilidadeDocumento.CompartilhadoCoordenacao
            ? db.Users.Where(u => u.Id == d.CompartilhadoPorId).Select(u => u.NomeCompleto).FirstOrDefault()
            : null,
        d.Visibilidade == VisibilidadeDocumento.CompartilhadoCoordenacao ? d.CompartilhadoEm : null,
        d.ArquivadoEm != null,
        Convert.ToBase64String(EF.Property<byte[]>(d, "Versao"))));

    /// <summary>Espelha <see cref="EscopoDocumentos.PodeVer"/>.</summary>
    private IQueryable<Documento> NoEscopo(EscopoDocumentos e)
    {
        var q = db.Documentos.AsNoTracking();
        if (!e.IncluirArquivados) q = q.Where(d => d.ArquivadoEm == null);
        if (e.SomenteCursoId is { } curso)
            q = q.Where(d => d.CursoId == curso && d.Visibilidade == VisibilidadeDocumento.CompartilhadoCoordenacao);
        return q;
    }
}
