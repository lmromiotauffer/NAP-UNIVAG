using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;

namespace Nap.Infrastructure.Persistencia.Consultas;

public sealed class AgendaQueries(NapDbContext db, IRelogio relogio, IUsuarioAtual? usuario = null) : IAgendaQueries
{
    public async Task<AgendaSemanaDto> ListarSemanaAsync(DateOnly segunda, Guid? profissionalId, CancellationToken ct)
    {
        var sexta = segunda.AddDays(4);
        var de = relogio.Fuso.InicioDoDia(segunda);
        // Até domingo: escala com faixa no sábado e trabalho autorizado fora da escala caem no fim de semana (a grade só mostra
        // sábado e domingo quando há item). Fim do DTO continua a sexta (semana útil).
        var ate = relogio.Fuso.InicioDoDia(segunda.AddDays(7));
        var itens = await Listar(db, de, ate, profissionalId, ct, usuario);
        return new AgendaSemanaDto(segunda, sexta, itens, await ListarAvulsos(de, ate, profissionalId, ct),
            await ListarAtividadesAvulsas(de, ate, profissionalId, ct));
    }

    /// <summary>Atendimentos avulsos Realizados/Ausentes (ocupam horário) que começam em [de, ate), sem dados clínicos.</summary>
    private async Task<IReadOnlyList<AtendimentoAvulsoAgendaDto>> ListarAvulsos(DateTimeOffset de, DateTimeOffset ate,
        Guid? profissionalId, CancellationToken ct)
    {
        var q = db.Atendimentos.AsNoTracking()
            .Where(a => a.CompromissoId == null && (a.Estado == EstadoAtendimento.Realizado || a.Estado == EstadoAtendimento.Ausente)
                && a.Intervalo.Inicio >= de && a.Intervalo.Inicio < ate);
        if (profissionalId is { } p) q = q.Where(a => a.ProfissionalId == p);

        return await q
            .OrderBy(a => a.Intervalo.Inicio).ThenBy(a => a.Id)
            .Select(a => new AtendimentoAvulsoAgendaDto(a.Id, a.Intervalo.Inicio, a.Intervalo.Fim, a.ProfissionalId,
                db.Users.Where(u => u.Id == a.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                a.AlunoId,
                db.Alunos.Where(l => l.Id == a.AlunoId).Select(l => l.NomeCompleto).FirstOrDefault() ?? "",
                a.Tipo, a.Estado))
            .ToListAsync(ct);
    }

    /// <summary>Atividades avulsas (ocupam horário) que começam em [de, ate); só tipo, sem descrição.</summary>
    private async Task<IReadOnlyList<AtividadeAvulsaAgendaDto>> ListarAtividadesAvulsas(DateTimeOffset de, DateTimeOffset ate,
        Guid? profissionalId, CancellationToken ct)
    {
        var q = db.AtividadesCurso.AsNoTracking()
            .Where(a => a.CompromissoId == null && a.Intervalo.Inicio >= de && a.Intervalo.Inicio < ate);
        if (profissionalId is { } p) q = q.Where(a => a.ProfissionalId == p);

        return await q
            .OrderBy(a => a.Intervalo.Inicio).ThenBy(a => a.Id)
            .Select(a => new AtividadeAvulsaAgendaDto(a.Id, a.Intervalo.Inicio, a.Intervalo.Fim, a.ProfissionalId,
                db.Users.Where(u => u.Id == a.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "", a.Tipo))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Compromissos vigentes (Agendados e Concluídos) que começam em [de, ate), projetados direto para DTO. O Concluído com
    /// atendimento (ou atividade) vinculado mostra o horário (e, no atendimento, o estado) do registro — ele é a fonte do tempo ocupado e pode ter sido
    /// corrigido depois (a realização registrada em reuniões/PEI/tarefas também vale como horário efetivo) — e é filtrado/ordenado por esse início efetivo, para agenda, painel e checagem de conflito concordarem.
    /// </summary>
    internal static async Task<IReadOnlyList<CompromissoDto>> Listar(NapDbContext db, DateTimeOffset de, DateTimeOffset ate,
        Guid? profissionalId, CancellationToken ct, IUsuarioAtual? usuario = null)
    {
        var q = db.Compromissos.AsNoTracking()
            .Where(c => c.Estado == EstadoCompromisso.Agendado || c.Estado == EstadoCompromisso.Concluido)
            // Pré-filtro pelo índice (ProfissionalId, Inicio) ou pela realização/registro vinculado na janela: o início efetivo
            // é um desses quatro, então o filtro exato abaixo não perde nada e as subconsultas não varrem a tabela inteira.
            .Where(c => (c.Intervalo.Inicio >= de && c.Intervalo.Inicio < ate)
                || (c.RealizacaoInicio >= de && c.RealizacaoInicio < ate)
                || db.Atendimentos.Any(a => a.Id == c.RegistroVinculadoId && a.Intervalo.Inicio >= de && a.Intervalo.Inicio < ate)
                || db.AtividadesCurso.Any(a => a.Id == c.RegistroVinculadoId && a.Intervalo.Inicio >= de && a.Intervalo.Inicio < ate));
        if (profissionalId is { } p) q = q.Where(c => c.ProfissionalId == p);

        var itens = await q
            .Select(c => new
            {
                c,
                Versao = EF.Property<byte[]>(c, "Versao"),
                Inicio = c.RealizacaoInicio
                    ?? db.Atendimentos.Where(a => a.Id == c.RegistroVinculadoId).Select(a => (DateTimeOffset?)a.Intervalo.Inicio)
                    .FirstOrDefault()
                    ?? db.AtividadesCurso.Where(a => a.Id == c.RegistroVinculadoId).Select(a => (DateTimeOffset?)a.Intervalo.Inicio)
                    .FirstOrDefault() ?? c.Intervalo.Inicio,
                Fim = c.RealizacaoFim
                    ?? db.Atendimentos.Where(a => a.Id == c.RegistroVinculadoId).Select(a => (DateTimeOffset?)a.Intervalo.Fim)
                    .FirstOrDefault()
                    ?? db.AtividadesCurso.Where(a => a.Id == c.RegistroVinculadoId).Select(a => (DateTimeOffset?)a.Intervalo.Fim)
                    .FirstOrDefault() ?? c.Intervalo.Fim,
            })
            .Where(x => x.Inicio >= de && x.Inicio < ate)
            .OrderBy(x => x.Inicio).ThenBy(x => x.c.Id)
            .Select(x => new CompromissoDto(x.c.Id, x.c.Tipo, x.c.Titulo, x.Inicio, x.Fim, x.c.ProfissionalId,
                db.Users.Where(u => u.Id == x.c.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
                x.c.AlunoId,
                db.Alunos.Where(a => a.Id == x.c.AlunoId).Select(a => a.NomeCompleto).FirstOrDefault(),
                x.c.Estado,
                db.Atendimentos.Where(a => a.Id == x.c.RegistroVinculadoId).Select(a => (EstadoAtendimento?)a.Estado).FirstOrDefault(),
                x.c.RemarcadoDeId, x.c.Descricao, Convert.ToBase64String(x.Versao), x.c.GrupoId, null))
            .ToListAsync(ct);

        var grupos = itens.Where(c => c.GrupoId is not null).Select(c => c.GrupoId!.Value).Distinct().ToList();
        if (grupos.Count == 0) return itens;
        var participantes = (await db.Compromissos.AsNoTracking()
            .Where(c => c.GrupoId != null && grupos.Contains(c.GrupoId.Value)
                && (c.Estado == EstadoCompromisso.Agendado || c.Estado == EstadoCompromisso.Concluido))
            .Select(c => new
            {
                GrupoId = c.GrupoId!.Value, c.ProfissionalId,
                Nome = db.Users.Where(u => u.Id == c.ProfissionalId).Select(u => u.NomeCompleto).FirstOrDefault() ?? "",
            })
            .Distinct()
            .ToListAsync(ct))
            .GroupBy(x => x.GrupoId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ParticipanteDto>)[.. g.OrderBy(x => x.Nome).ThenBy(x => x.ProfissionalId)
                .Select(x => new ParticipanteDto(x.ProfissionalId, x.Nome))]);
        // Uma consulta para todos os grupos da janela: o criador é o das instâncias originais (a mesma regra de escopo=Grupo).
        var criadores = usuario is null ? [] : (await db.Compromissos.AsNoTracking()
            .Where(c => c.GrupoId != null && grupos.Contains(c.GrupoId.Value) && c.RemarcadoDeId == null)
            .Select(c => new { GrupoId = c.GrupoId!.Value, c.CriadoPorId })
            .Distinct()
            .ToListAsync(ct))
            .GroupBy(x => x.GrupoId)
            .ToDictionary(g => g.Key, g => g.First().CriadoPorId);
        return [.. itens.Select(c => c.GrupoId is { } g
            ? c with
            {
                Participantes = participantes.GetValueOrDefault(g, []),
                PodeGerirGrupo = usuario is not null && criadores.TryGetValue(g, out var criador) && GrupoCompromisso.PodeGerirGrupo(usuario, criador),
            }
            : c)];
    }
}
