using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Encaminhamentos;
using Nap.Domain.Encaminhamentos;

namespace Nap.Infrastructure.Persistencia.Consultas;

public sealed class EncaminhamentoQueries(NapDbContext db) : IEncaminhamentoQueries
{
    public async Task<PagedResult<EncaminhamentoListaDto>> ListarAsync(FiltroEncaminhamentos f, EscopoEncaminhamentos e,
        DateOnly hoje, int diasSemAcolhimento, CancellationToken ct)
    {
        // "Sem acolhimento no prazo": Encaminhado e DataEncaminhamento + dias < hoje.
        var limite = hoje.AddDays(-diasSemAcolhimento);
        var tudoVisivel = e.SomenteCursoId is null;
        var q = NoEscopo(db, e);
        // Filtro por especialidade/destino não pode revelar o que a lista oculta do coordenador de curso.
        if (!tudoVisivel && (f.Especialidade is not null || f.Destino is not null))
            q = q.Where(x => x.Destino == DestinoEncaminhamento.CoordenacaoCurso);
        if (f.AlunoId is { } aluno) q = q.Where(x => x.AlunoId == aluno);
        if (f.CursoId is { } curso) q = q.Where(x => x.CursoId == curso);
        if (f.Especialidade is { } especialidade) q = q.Where(x => x.Especialidade == especialidade);
        if (f.Destino is { } destino) q = q.Where(x => x.Destino == destino);
        if (f.Estado is { } estado) q = q.Where(x => x.Estado == estado);
        if (f.De is { } de) q = q.Where(x => x.DataEncaminhamento >= de);
        if (f.Ate is { } ate) q = q.Where(x => x.DataEncaminhamento <= ate);
        if (f.SemAcolhimentoNoPrazo is { } sem)
            q = sem
                ? q.Where(x => x.Estado == EstadoEncaminhamento.Encaminhado && x.DataEncaminhamento < limite)
                : q.Where(x => !(x.Estado == EstadoEncaminhamento.Encaminhado && x.DataEncaminhamento < limite));

        var total = await q.CountAsync(ct);
        var itens = await q
            .OrderByDescending(x => x.DataEncaminhamento).ThenByDescending(x => x.Id)
            .Skip((f.Pagina - 1) * f.Tamanho).Take(f.Tamanho)
            .Select(x => new EncaminhamentoListaDto(x.Id, x.AlunoId,
                db.Alunos.Where(a => a.Id == x.AlunoId).Select(a => a.NomeCompleto).FirstOrDefault() ?? "",
                db.Alunos.Where(a => a.Id == x.AlunoId).Select(a => a.Matricula.Valor).FirstOrDefault() ?? "",
                db.Cursos.Where(c => c.Id == x.CursoId).Select(c => c.Nome).FirstOrDefault() ?? "",
                tudoVisivel || x.Destino == DestinoEncaminhamento.CoordenacaoCurso ? x.Especialidade : null,
                tudoVisivel || x.Destino == DestinoEncaminhamento.CoordenacaoCurso ? x.Destino : null,
                x.Estado, x.DataEncaminhamento,
                db.Users.Where(u => u.Id == x.SolicitanteId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                x.Estado == EstadoEncaminhamento.Encaminhado && x.DataEncaminhamento < limite, x.AtualizadoEm))
            .ToListAsync(ct);
        return new PagedResult<EncaminhamentoListaDto>(itens, f.Pagina, f.Tamanho, total);
    }

    public async Task<EncaminhamentoDetalheDto?> ObterAsync(Guid id, EscopoEncaminhamentos e, IUsuarioAtual u, DateOnly hoje,
        int diasSemAcolhimento, CancellationToken ct)
    {
        var limite = hoje.AddDays(-diasSemAcolhimento);
        var verMotivo = e.VerMotivo;
        var verObservacoes = e.VerObservacoes;
        // Coordenador de curso só lê especialidade, destino e textos de evolução quando o destino é a própria coordenação.
        var textoSempre = e.SomenteCursoId is null;
        var linha = await NoEscopo(db, e)
            .Where(x => x.Id == id)
            .Select(x => new { x.CursoId, x.Destino, Dto = new EncaminhamentoDetalheDto(x.Id, x.AlunoId,
                db.Alunos.Where(a => a.Id == x.AlunoId).Select(a => a.NomeCompleto).FirstOrDefault() ?? "",
                db.Alunos.Where(a => a.Id == x.AlunoId).Select(a => a.Matricula.Valor).FirstOrDefault() ?? "",
                db.Cursos.Where(c => c.Id == x.CursoId).Select(c => c.Nome).FirstOrDefault() ?? "",
                textoSempre || x.Destino == DestinoEncaminhamento.CoordenacaoCurso ? x.Especialidade : null,
                textoSempre || x.Destino == DestinoEncaminhamento.CoordenacaoCurso ? x.Destino : null,
                x.Estado, x.DataEncaminhamento,
                db.Users.Where(us => us.Id == x.SolicitanteId).Select(us => us.NomeCompleto).FirstOrDefault() ?? "",
                x.Estado == EstadoEncaminhamento.Encaminhado && x.DataEncaminhamento < limite, x.AtualizadoEm,
                x.SolicitanteId, verMotivo ? x.MotivoRestrito : null, x.ResumoCompartilhavel,
                verObservacoes ? x.Observacoes : null,
                x.DataAcolhimento, x.DataPrimeiroAtendimento, x.DataEncerramento, false, false,
                x.Evolucoes.OrderBy(v => v.Em).ThenBy(v => v.Id)
                    .Select(v => new EvolucaoEncaminhamentoDto(v.Em,
                        db.Users.Where(us => us.Id == v.AutorId).Select(us => us.NomeCompleto).FirstOrDefault() ?? "",
                        v.Acao, v.EstadoNovo, v.Data,
                        textoSempre || x.Destino == DestinoEncaminhamento.CoordenacaoCurso ? v.Texto : null))
                    .ToList(),
                Convert.ToBase64String(EF.Property<byte[]>(x, "Versao"))) })
            .SingleOrDefaultAsync(ct);
        if (linha is null) return null;

        var dto = linha.Dto;
        var aberto = dto.Estado is not (EstadoEncaminhamento.Alta or EstadoEncaminhamento.Desistencia or EstadoEncaminhamento.Cancelado);
        return dto with
        {
            PodeRegistrarRetorno = aberto && AutorizacaoEncaminhamento.PodeRegistrarRetorno(u, linha.Destino, linha.CursoId),
            PodeCancelar = aberto && u.EhEquipeNap(),
        };
    }

    /// <summary>Recorte por perfil, compartilhado com o painel.</summary>
    internal static IQueryable<Encaminhamento> NoEscopo(NapDbContext db, EscopoEncaminhamentos e)
    {
        var q = db.Encaminhamentos.AsNoTracking();
        if (e.Todos) return q;
        if (e.SoDestinosSip) return q.Where(x => x.Destino == DestinoEncaminhamento.Sip || x.Destino == DestinoEncaminhamento.ClinicaEscola);
        if (e.SomenteCursoId is { } curso) return q.Where(x => x.CursoId == curso);
        return q.Where(_ => false); // escopo sem recorte reconhecido: falha fechada
    }
}
