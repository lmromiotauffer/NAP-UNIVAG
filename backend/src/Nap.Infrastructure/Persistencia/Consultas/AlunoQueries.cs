using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Domain.Alunos;
using Nap.Domain.Atendimentos;
using Nap.Domain.Encaminhamentos;

namespace Nap.Infrastructure.Persistencia.Consultas;

public sealed class AlunoQueries(NapDbContext db) : IAlunoQueries
{
    private const string AutorNaoRegistrado = "Não registrado";

    public async Task<PagedResult<AlunoListaDto>> ListarAsync(FiltroAlunos f, EscopoAlunos e, CancellationToken ct)
    {
        var q = NoEscopo(e);
        // Ex-coordenador (aluno transferido): o estado atual não é dele, nem para filtrar; só casa quem ainda está no curso dele.
        var somente = e.SomenteCursoId;
        if (!string.IsNullOrWhiteSpace(f.Busca))
        {
            var busca = f.Busca.Trim();
            q = q.Where(a => a.NomeCompleto.Contains(busca) || a.Matricula.Valor.Contains(busca));
        }
        if (f.CursoId is { } curso) q = q.Where(a => (somente == null || a.CursoAtualId == somente) && a.CursoAtualId == curso);
        if (f.ProfissionalId is { } prof) q = q.Where(a => (somente == null || a.CursoAtualId == somente) && a.ProfissionalResponsavelId == prof);
        if (f.Situacao is { } situacao) q = q.Where(a => (somente == null || a.CursoAtualId == somente) && a.Situacao == situacao);
        if (f.InicioDe is { } de) q = q.Where(a => a.InicioAcompanhamento >= de);
        if (f.InicioAte is { } ate) q = q.Where(a => a.InicioAcompanhamento <= ate);

        if (f.EspecialidadeEncaminhamento is { } especialidade)
        {
            // Mesmo recorte de ListarEncaminhamentos: coordenador de curso só casa encaminhamentos de destino
            // CoordenacaoCurso gravados com o curso dele, para não revelar por exclusão o que a lista oculta.
            var restrito = e.SomenteCursoId;
            q = q.Where(a => db.Encaminhamentos.Any(x => x.AlunoId == a.Id && x.Especialidade == especialidade
                && (restrito == null || (x.CursoId == restrito && x.Destino == DestinoEncaminhamento.CoordenacaoCurso))));
        }

        var total = await q.CountAsync(ct);
        var clinico = e.IncluirClinico;
        // Totais por subconsulta correlacionada agregada: uma única ida ao banco. Faltas = Ausentes de tipos individuais
        // (reunião fora), a mesma regra do painel.
        var itens = await q
            .OrderBy(a => a.NomeCompleto).ThenBy(a => a.Id)
            .Skip((f.Pagina - 1) * f.Tamanho).Take(f.Tamanho)
            .Select(a => new AlunoListaDto(a.Id, a.NomeCompleto, a.Matricula.Valor,
                somente == null || a.CursoAtualId == somente ? a.MatriculaAtiva : null,
                somente == null || a.CursoAtualId == somente ? a.CursoAtualId : null,
                somente == null || a.CursoAtualId == somente
                    ? db.Cursos.Where(c => c.Id == a.CursoAtualId).Select(c => c.Nome).FirstOrDefault() ?? "" : "",
                somente == null || a.CursoAtualId == somente ? a.Semestre : null,
                somente == null || a.CursoAtualId == somente ? a.ProfissionalResponsavelId : null,
                somente == null || a.CursoAtualId == somente
                    ? db.Users.Where(u => u.Id == a.ProfissionalResponsavelId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "" : "",
                somente == null || a.CursoAtualId == somente ? a.Situacao : null,
                clinico ? db.Atendimentos.Count(x => x.AlunoId == a.Id && x.Estado == EstadoAtendimento.Realizado) : null,
                clinico ? db.Atendimentos.Count(x => x.AlunoId == a.Id && x.Estado == EstadoAtendimento.Ausente && x.Tipo != TipoAtendimento.Reuniao) : null))
            .ToListAsync(ct);
        return new PagedResult<AlunoListaDto>(itens, f.Pagina, f.Tamanho, total);
    }

    public async Task<AlunoFichaDto?> ObterFichaAsync(Guid id, EscopoAlunos e, CancellationToken ct)
    {
        // Coordenador de curso não recebe o resumo de acompanhamento (dado clínico): os agregados nem são calculados.
        var clinico = e.IncluirClinico;
        var linha = await NoEscopo(e)
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id, a.NomeCompleto, Matricula = a.Matricula.Valor, a.MatriculaAtiva, a.CursoAtualId,
                Curso = db.Cursos.Where(c => c.Id == a.CursoAtualId).Select(c => c.Nome).FirstOrDefault(),
                a.Semestre, a.ProfissionalResponsavelId,
                Profissional = db.Users.Where(u => u.Id == a.ProfissionalResponsavelId).Select(u => u.NomeCompleto).FirstOrDefault(),
                a.Situacao, a.InicioAcompanhamento, a.NecessidadesEducacionais, a.ObservacoesAcademicas,
                Realizados = clinico ? db.Atendimentos.Count(x => x.AlunoId == a.Id && x.Estado == EstadoAtendimento.Realizado) : 0,
                Faltas = clinico ? db.Atendimentos.Count(x => x.AlunoId == a.Id && x.Estado == EstadoAtendimento.Ausente && x.Tipo != TipoAtendimento.Reuniao) : 0,
                Ultimo = clinico
                    ? db.Atendimentos.Where(x => x.AlunoId == a.Id && x.Estado == EstadoAtendimento.Realizado).Max(x => (DateTimeOffset?)x.Intervalo.Inicio)
                    : null,
                Versao = EF.Property<byte[]>(a, "Versao"),
            })
            .SingleOrDefaultAsync(ct);
        if (linha is null) return null;

        // Ex-coordenador (aluno já em outro curso): nada do estado atual, que foi definido depois do vínculo dele.
        if (e.SomenteCursoId is { } somente && linha.CursoAtualId != somente)
            return new AlunoFichaDto(linha.Id, linha.NomeCompleto, linha.Matricula, null, null, "", null,
                null, "", null, linha.InicioAcompanhamento, null, null, null, Convert.ToBase64String(linha.Versao));

        return new AlunoFichaDto(linha.Id, linha.NomeCompleto, linha.Matricula, linha.MatriculaAtiva, linha.CursoAtualId, linha.Curso ?? "", linha.Semestre,
            linha.ProfissionalResponsavelId, linha.Profissional ?? "", linha.Situacao, linha.InicioAcompanhamento,
            linha.NecessidadesEducacionais, linha.ObservacoesAcademicas,
            clinico ? new ResumoAcompanhamentoDto(linha.Realizados, linha.Faltas, linha.Ultimo) : null,
            Convert.ToBase64String(linha.Versao));
    }

    /// <summary>
    /// Mescla mudanças de situação, vínculos de curso, encaminhamentos (criação e retornos) e (só com dados clínicos)
    /// atendimentos. Cada fonte devolve, já ordenadas no banco, no máximo as <c>pagina × tamanho</c> mais recentes — o bastante para montar a página
    /// exata em memória; o total é a soma das contagens. Coordenador de curso (<c>SomenteCursoId</c>) só vê eventos da janela
    /// do vínculo mais recente com o curso DELE: do registro desse vínculo até o registro do vínculo seguinte (o histórico de
    /// outro curso não é do curso dele), sem motivo de situação/vínculo (<c>Resumo</c> nulo, pode conter informação sensível) e, nos
    /// encaminhamentos, com resumo compartilhável e texto de evolução só quando o destino é a própria coordenação (mesma regra do
    /// detalhe em <see cref="EncaminhamentoQueries"/>). <c>null</c> quando o aluno não existe ou está fora do escopo.
    /// </summary>
    public async Task<PagedResult<EventoLinhaDoTempoDto>?> LinhaDoTempoAsync(Guid alunoId, EscopoAlunos e, int pagina, int tamanho, CancellationToken ct)
    {
        var vazio = new PagedResult<EventoLinhaDoTempoDto>([], pagina, tamanho, 0);
        if (!await NoEscopo(e).AnyAsync(a => a.Id == alunoId, ct)) return null;
        var limite = pagina * tamanho;
        var aluno = db.Alunos.AsNoTracking().Where(a => a.Id == alunoId);
        var restrito = e.SomenteCursoId is not null;

        var situacoes = aluno.SelectMany(a => a.HistoricoSituacao);
        var cursos = aluno.SelectMany(a => a.HistoricoCursos);
        var encaminhamentos = db.Encaminhamentos.AsNoTracking().Where(x => x.AlunoId == alunoId);
        var evolucoes = encaminhamentos.SelectMany(x => x.Evolucoes, (x, v) => new { x.Destino, Evolucao = v });
        if (restrito)
        {
            var corte = await cursos.Where(v => v.CursoId == e.SomenteCursoId).MaxAsync(v => (DateTimeOffset?)v.RegistradoEm, ct);
            if (corte is null) return vazio; // sem vínculo com o curso dele: falha fechada, nada de histórico sem corte
            // Fim da janela: o vínculo seguinte (transferência para outro curso), que já é período de outro coordenador.
            // Nulo quando o curso dele é o atual.
            // shortcut: só o vínculo mais recente com o curso dele conta; um período anterior no mesmo curso (A → B → A) fica de fora.
            var fimJanela = await cursos.Where(v => v.RegistradoEm > corte).MinAsync(v => (DateTimeOffset?)v.RegistradoEm, ct);
            situacoes = situacoes.Where(m => m.Em >= corte && (fimJanela == null || m.Em < fimJanela));
            cursos = cursos.Where(v => v.RegistradoEm >= corte && (fimJanela == null || v.RegistradoEm < fimJanela));
            // Só encaminhamentos gravados com o curso dele (o curso da criação, não o atual do aluno).
            encaminhamentos = encaminhamentos.Where(x => x.CursoId == e.SomenteCursoId);
            // Mesma janela das situações: retorno registrado depois da transferência é período de outro coordenador.
            evolucoes = encaminhamentos.SelectMany(x => x.Evolucoes, (x, v) => new { x.Destino, Evolucao = v })
                .Where(p => p.Evolucao.Em >= corte && (fimJanela == null || p.Evolucao.Em < fimJanela));
            encaminhamentos = encaminhamentos.Where(x => x.CriadoEm >= corte && (fimJanela == null || x.CriadoEm < fimJanela));
        }
        var totalSituacoes = await situacoes.CountAsync(ct);
        var eventos = (await situacoes
                .OrderByDescending(m => m.Em).ThenByDescending(m => m.Id).Take(limite)
                .Select(m => new
                {
                    m.Id, m.Em, m.De, m.Para, m.Motivo,
                    Autor = db.Users.Where(u => u.Id == m.AutorId).Select(u => u.NomeCompleto).FirstOrDefault(),
                })
                .ToListAsync(ct))
            .Select(m => (m.Id, Evento: new EventoLinhaDoTempoDto(m.Em, "situacao",
                m.De is { } de ? $"Situação alterada de {Rotulos.De(de)} para {Rotulos.De(m.Para)}" : $"Cadastro com situação {Rotulos.De(m.Para)}",
                restrito ? null : m.Motivo, m.Autor ?? AutorNaoRegistrado)))
            .ToList();

        // Vínculo de curso entra pelo instante em que foi registrado; a data de início vai no título.
        var totalCursos = await cursos.CountAsync(ct);
        eventos.AddRange((await cursos
                .OrderByDescending(v => v.RegistradoEm).ThenByDescending(v => v.Id).Take(limite)
                .Select(v => new
                {
                    v.Id, v.Desde, v.RegistradoEm, v.Motivo,
                    Curso = db.Cursos.Where(c => c.Id == v.CursoId).Select(c => c.Nome).FirstOrDefault(),
                    Autor = db.Users.Where(u => u.Id == v.AutorId).Select(u => u.NomeCompleto).FirstOrDefault(),
                })
                .ToListAsync(ct))
            .Select(v => (v.Id, new EventoLinhaDoTempoDto(v.RegistradoEm, "curso",
                $"Vínculo com o curso {v.Curso} desde {v.Desde:dd'/'MM'/'yyyy}", restrito ? null : v.Motivo, v.Autor ?? AutorNaoRegistrado))));

        // Criação e retornos de encaminhamento. Coordenador de curso só vê especialidade, destino, resumo compartilhável e
        // textos de evolução quando o destino é a própria coordenação (mesma regra do detalhe do encaminhamento).
        var totalEncaminhamentos = await encaminhamentos.CountAsync(ct);
        eventos.AddRange((await encaminhamentos
                .OrderByDescending(x => x.CriadoEm).ThenByDescending(x => x.Id).Take(limite)
                .Select(x => new
                {
                    x.Id, x.CriadoEm,
                    Especialidade = !restrito || x.Destino == DestinoEncaminhamento.CoordenacaoCurso ? x.Especialidade : (Especialidade?)null,
                    Destino = !restrito || x.Destino == DestinoEncaminhamento.CoordenacaoCurso ? x.Destino : (DestinoEncaminhamento?)null,
                    Resumo = !restrito || x.Destino == DestinoEncaminhamento.CoordenacaoCurso ? x.ResumoCompartilhavel : null,
                    Autor = db.Users.Where(u => u.Id == x.SolicitanteId).Select(u => u.NomeCompleto).FirstOrDefault(),
                })
                .ToListAsync(ct))
            .Select(x => (x.Id, new EventoLinhaDoTempoDto(x.CriadoEm, "encaminhamento",
                x.Especialidade is { } especialidade && x.Destino is { } destino
                    ? $"Encaminhamento para {Rotulos.De(especialidade)} · {Rotulos.De(destino)}"
                    : "Encaminhamento",
                x.Resumo, x.Autor ?? AutorNaoRegistrado))));

        var totalEvolucoes = await evolucoes.CountAsync(ct);
        eventos.AddRange((await evolucoes
                .OrderByDescending(p => p.Evolucao.Em).ThenByDescending(p => p.Evolucao.Id).Take(limite)
                .Select(p => new
                {
                    p.Evolucao.Id, p.Evolucao.Em, p.Evolucao.Acao,
                    Texto = !restrito || p.Destino == DestinoEncaminhamento.CoordenacaoCurso ? p.Evolucao.Texto : null,
                    Autor = db.Users.Where(u => u.Id == p.Evolucao.AutorId).Select(u => u.NomeCompleto).FirstOrDefault(),
                })
                .ToListAsync(ct))
            .Select(v => (v.Id, new EventoLinhaDoTempoDto(v.Em, "encaminhamento", $"Encaminhamento: {Rotulos.De(v.Acao)}",
                v.Texto, v.Autor ?? AutorNaoRegistrado))));

        var totalAtendimentos = 0;
        if (e.IncluirClinico)
        {
            // Resumo = tipo e estado; nunca observações privadas.
            var atendimentos = db.Atendimentos.AsNoTracking().Where(a => a.AlunoId == alunoId);
            totalAtendimentos = await atendimentos.CountAsync(ct);
            eventos.AddRange((await atendimentos
                    .OrderByDescending(a => a.Intervalo.Inicio).ThenByDescending(a => a.Id).Take(limite)
                    .Select(a => new
                    {
                        a.Id, a.Intervalo.Inicio, a.Tipo, a.Estado,
                        Autor = db.Users.Where(u => u.Id == a.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault(),
                    })
                    .ToListAsync(ct))
                .Select(a => (a.Id, new EventoLinhaDoTempoDto(a.Inicio, "atendimento", $"Atendimento: {Rotulos.De(a.Tipo)}",
                    $"{Rotulos.De(a.Tipo)} · {a.Estado}", a.Autor ?? AutorNaoRegistrado))));
        }

        var itens = eventos
            .OrderByDescending(x => x.Evento.Em).ThenByDescending(x => x.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(x => x.Evento)
            .ToList();
        return new PagedResult<EventoLinhaDoTempoDto>(itens, pagina, tamanho,
            totalSituacoes + totalCursos + totalEncaminhamentos + totalEvolucoes + totalAtendimentos);
    }

    private IQueryable<Aluno> NoEscopo(EscopoAlunos e)
    {
        var q = db.Alunos.AsNoTracking();
        // Curso da época, como encaminhamentos, documentos e PEI: quem coordenou o aluno antes da transferência segue
        // chegando à ficha (e aos registros gravados com o curso dele). A linha do tempo recorta à janela desse vínculo;
        // ficha e lista omitem o estado atual.
        return e.SomenteCursoId is { } curso ? q.Where(PassouPeloCurso(curso)) : q;
    }

    /// <summary>
    /// O aluno teve vínculo com o curso: recorte do coordenador na ficha, nos documentos e nos PEIs. Vínculo de duração zero
    /// (correção de curso no mesmo dia) não conta, como no painel.
    /// </summary>
    internal static Expression<Func<Aluno, bool>> PassouPeloCurso(Guid curso) =>
        a => a.HistoricoCursos.Any(v => v.CursoId == curso && (v.Ate == null || v.Ate != v.Desde));
}
