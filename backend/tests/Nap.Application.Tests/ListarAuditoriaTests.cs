using Nap.Application.Comum;
using Nap.Application.Features.Auditoria;
using Nap.Application.Features.Auditoria.Queries;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Comum;

namespace Nap.Application.Tests;

public class ListarAuditoriaTests
{
    private sealed class AuditoriaQueriesFalso : IAuditoriaQueries
    {
        public FiltroAuditoria? Filtro { get; private set; }

        public Task<PagedResult<EventoAuditoriaDto>> ListarAsync(FiltroAuditoria filtro, CancellationToken ct)
        {
            Filtro = filtro;
            return Task.FromResult(new PagedResult<EventoAuditoriaDto>([], filtro.Pagina, filtro.Tamanho, 0));
        }
    }

    private readonly AuditoriaQueriesFalso _consultas = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new();

    private Task<PagedResult<EventoAuditoriaDto>> Listar(string perfil, ListarAuditoriaQuery query) =>
        new ListarAuditoriaHandler(new ListarAuditoriaValidator(), _consultas, new UsuarioAtualFalso(perfil), _auditoria, _uow)
            .Handle(query, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Periodo_acima_de_366_dias_retorna_400()
    {
        var longo = await Assert.ThrowsAsync<ValidationException>(() =>
            Listar(Perfis.Administrador, new ListarAuditoriaQuery(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 3))));
        Assert.Contains("fim", longo.Erros.Keys);
        var invertido = await Assert.ThrowsAsync<ValidationException>(() =>
            Listar(Perfis.Administrador, new ListarAuditoriaQuery(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 31))));
        Assert.Contains("fim", invertido.Erros.Keys);
        var acima = await Assert.ThrowsAsync<ValidationException>(() =>
            Listar(Perfis.Administrador, new ListarAuditoriaQuery(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 2)))); // 367 dias
        Assert.Contains("fim", acima.Erros.Keys);
        await Assert.ThrowsAsync<ValidationException>(() =>
            Listar(Perfis.Administrador, new ListarAuditoriaQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), Tamanho: 101)));

        await Listar(Perfis.Administrador, new ListarAuditoriaQuery(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1))); // 366 dias
        Assert.DoesNotContain(_auditoria.Registros, r => r.Acao != "auditoria.consulta");
    }

    [Theory]
    [InlineData(true, "inicio")]
    [InlineData(false, "fim")]
    public async Task Periodo_sem_inicio_ou_fim_retorna_400(bool semInicio, string campo)
    {
        DateOnly? data = new DateOnly(2026, 9, 1);
        var erro = await Assert.ThrowsAsync<ValidationException>(() =>
            Listar(Perfis.Administrador, new ListarAuditoriaQuery(semInicio ? null : data, semInicio ? data : null)));
        Assert.Equal([campo], erro.Erros.Keys);
    }

    [Fact]
    public async Task Profissional_recebe_403_na_consulta()
    {
        foreach (var perfil in new[] { Perfis.ProfissionalNap, Perfis.CoordenadorCurso, Perfis.Sip, Perfis.Gestao })
            await Assert.ThrowsAsync<ForbiddenException>(() => Listar(perfil, new ListarAuditoriaQuery(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30))));
        Assert.Null(_consultas.Filtro);
        Assert.Empty(_auditoria.Registros);
    }

    [Fact]
    public async Task Consulta_e_auditada()
    {
        var usuarioId = Guid.NewGuid();
        await Listar(Perfis.CoordenadorNap, new ListarAuditoriaQuery(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), usuarioId,
            "pei.", "Pei", null, "negado", 2, 50));

        Assert.Equal(new FiltroAuditoria(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), usuarioId, "pei.", "Pei", null, "negado", 2, 50),
            _consultas.Filtro);
        Assert.Equal(("auditoria.consulta", "Auditoria", (Guid?)null), Assert.Single(_auditoria.Registros));
        Assert.Equal(1, _uow.Salvamentos);
    }
}
