using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Atividades;
using Nap.Domain.Atividades;

namespace Nap.Infrastructure.Persistencia.Consultas;

public sealed class AtividadeQueries(NapDbContext db, IRelogio relogio) : IAtividadeQueries
{
    public async Task<PagedResult<AtividadeDto>> ListarAsync(FiltroAtividades f, CancellationToken ct)
    {
        var q = db.AtividadesCurso.AsNoTracking().AsQueryable();
        // Recorte antes de contar e paginar.
        if (f.CursoRecorte is { } recorte) q = q.Where(a => a.CursoId == recorte);
        if (f.CursoId is { } curso) q = q.Where(a => a.CursoId == curso);
        if (f.ProfissionalId is { } prof) q = q.Where(a => a.ProfissionalId == prof);
        if (f.Tipo is { } tipo) q = q.Where(a => a.Tipo == tipo);
        if (f.Institucionais is { } institucionais)
            q = institucionais ? q.Where(a => a.CursoId == null) : q.Where(a => a.CursoId != null);
        if (f.Inicio is { } de)
        {
            var inicio = relogio.Fuso.InicioDoDia(de);
            q = q.Where(a => a.Intervalo.Inicio >= inicio);
        }
        if (f.Fim is { } ate)
        {
            var fim = relogio.Fuso.InicioDoDia(ate.AddDays(1));
            q = q.Where(a => a.Intervalo.Inicio < fim);
        }

        var total = await q.CountAsync(ct);
        var linhas = await q
            .OrderByDescending(a => a.Intervalo.Inicio).ThenByDescending(a => a.Id)
            .Skip((f.Pagina - 1) * f.Tamanho).Take(f.Tamanho)
            .Select(a => new
            {
                a.Id, a.Intervalo.Inicio, a.Intervalo.Fim, a.CursoId,
                Curso = db.Cursos.Where(c => c.Id == a.CursoId).Select(c => c.Nome).FirstOrDefault(),
                a.ProfissionalId,
                Profissional = db.Users.Where(u => u.Id == a.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                a.Tipo, a.Descricao, Avulsa = a.CompromissoId == null,
                Versao = EF.Property<byte[]>(a, "Versao"),
            })
            .ToListAsync(ct);

        var itens = linhas.Select(l =>
        {
            var (data, turno, duracao) = DataTurnoDuracao(l.Inicio, l.Fim);
            return new AtividadeDto(l.Id, data, turno, l.Inicio, l.Fim, duracao, l.CursoId, l.Curso, l.ProfissionalId,
                l.Profissional, l.Tipo, l.Descricao, l.Avulsa, Convert.ToBase64String(l.Versao));
        }).ToList();
        return new PagedResult<AtividadeDto>(itens, f.Pagina, f.Tamanho, total);
    }

    public async Task<AtividadeDetalheDto?> ObterAsync(Guid id, Guid? cursoRecorte, CancellationToken ct)
    {
        var q = db.AtividadesCurso.AsNoTracking().Where(a => a.Id == id);
        if (cursoRecorte is { } recorte) q = q.Where(a => a.CursoId == recorte);
        var l = await q.Select(a => new
        {
            a.Id, a.Intervalo.Inicio, a.Intervalo.Fim, a.CursoId,
            Curso = db.Cursos.Where(c => c.Id == a.CursoId).Select(c => c.Nome).FirstOrDefault(),
            a.ProfissionalId,
            Profissional = db.Users.Where(u => u.Id == a.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
            a.Tipo, a.Descricao, a.JustificativaAvulso, a.CompromissoId,
            Versao = EF.Property<byte[]>(a, "Versao"),
            // Coordenação de curso: só as correções cujo estado anterior já era do próprio curso.
            Correcoes = a.Correcoes.Where(c => cursoRecorte == null || c.CursoIdAnterior == cursoRecorte).OrderBy(c => c.Em).ThenBy(c => c.Id).Select(c => new CorrecaoAtividadeDto(
                db.Users.Where(u => u.Id == c.AutorId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                c.Em, c.Justificativa, c.InicioAnterior, c.FimAnterior, c.CursoIdAnterior,
                db.Cursos.Where(x => x.Id == c.CursoIdAnterior).Select(x => x.Nome).FirstOrDefault(),
                c.TipoAnterior, c.DescricaoAnterior)).ToList(),
        }).SingleOrDefaultAsync(ct);
        if (l is null) return null;

        var (data, turno, duracao) = DataTurnoDuracao(l.Inicio, l.Fim);
        return new AtividadeDetalheDto(l.Id, data, turno, l.Inicio, l.Fim, duracao, l.CursoId, l.Curso, l.ProfissionalId,
            l.Profissional, l.Tipo, l.Descricao, l.CompromissoId == null, Convert.ToBase64String(l.Versao),
            l.JustificativaAvulso, l.CompromissoId, l.Correcoes, false);
    }

    private (DateOnly Data, Turno Turno, int Duracao) DataTurnoDuracao(DateTimeOffset inicio, DateTimeOffset fim)
    {
        return (relogio.Fuso.DataLocal(inicio), AtividadeCurso.TurnoDe(relogio.Fuso.HoraLocal(inicio)), (int)(fim - inicio).TotalMinutes);
    }
}
