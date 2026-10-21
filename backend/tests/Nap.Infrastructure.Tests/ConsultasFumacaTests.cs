using Microsoft.EntityFrameworkCore;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Atendimentos;
using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum.ValueObjects;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Tests;

/// <summary>Executa uma vez cada consulta não coberta em outro teste: falhas de tradução do EF só aparecem em execução.</summary>
public class ConsultasFumacaTests(SqlServerFixture fx)
{
    [Fact]
    public async Task Demais_consultas_traduzem_e_os_indices_manuais_existem()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: "Profissional fumaça");
        var curso = await fx.CriarCursoAsync("Curso fumaça");
        var aluno = await RoundTripTests.CriarAlunoAsync(fx, curso, prof);
        var segunda = new DateOnly(2026, 9, 14);
        var inicio = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(-4));
        var agora = inicio.AddDays(1);
        await using (var db = fx.CriarContexto())
        {
            db.AddRange(
                Atendimento.RegistrarAvulso(aluno.Id, prof, IntervaloHorario.Criar(inicio, inicio.AddMinutes(45)), TipoAtendimento.Acompanhamento,
                    EstadoAtendimento.Realizado, new DadosClinicos("Demanda", "Privado", null, null), "Justificativa avulso", prof, agora),
                Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião", IntervaloHorario.Criar(inicio.AddHours(2), inicio.AddHours(3)),
                    prof, null, null, prof, agora));
            await db.SaveChangesAsync(ct);
        }

        var sql = new List<string>();
        await using var leitura = fx.CriarContexto(sql.Add);
        var relogio = SqlServerFixture.Relogio();
        var alunos = new AlunoQueries(leitura);
        var ficha = await alunos.ObterFichaAsync(aluno.Id, new EscopoAlunos(null, true), ct);
        var fichaCurso = await alunos.ObterFichaAsync(aluno.Id, new EscopoAlunos(curso, false), ct);
        var atendimentos = await new AtendimentoQueries(leitura, relogio).ListarAsync(
            new FiltroAtendimentos(aluno.Id, prof, EstadoAtendimento.Realizado, TipoAtendimento.Acompanhamento, segunda, segunda.AddDays(4), 1, 10), ct);
        var semana = await new AgendaQueries(leitura, relogio).ListarSemanaAsync(segunda, prof, ct);
        var usuarios = new UsuarioQueries(leitura);
        var profissionais = await usuarios.ListarProfissionaisAsync(false, ct);
        var cursos = new CursoQueries(leitura);
        var listaCursos = await cursos.ListarAsync(ct);
        var cursoAtivo = await cursos.CursoAtivoExisteAsync(curso, ct);
        var indices = await leitura.Database.SqlQuery<string>($"""
            SELECT name AS Value FROM sys.indexes WHERE name IN
            ('IX_Compromissos_ProfissionalId_Inicio', 'IX_Compromissos_AlunoId_Inicio', 'IX_Atendimentos_AlunoId_Inicio', 'IX_Atendimentos_ProfissionalId_Inicio')
            """).ToListAsync(ct);
        foreach (var s in sql) TestContext.Current.TestOutputHelper?.WriteLine(s);

        Assert.Equal((1, 0, inicio), (ficha!.Resumo!.TotalAtendimentos, ficha.Resumo.TotalFaltas, ficha.Resumo.UltimoAtendimento!.Value));
        Assert.Equal("Profissional fumaça", ficha.Profissional);
        Assert.Null(fichaCurso!.Resumo);
        var item = Assert.Single(atendimentos.Itens);
        Assert.Equal((45, "Demanda", true), (item.DuracaoMinutos, item.DemandaPrincipal, item.Avulso));
        Assert.Equal("Reunião", Assert.Single(semana.Compromissos).Titulo);
        Assert.Contains(profissionais, p => p.Id == prof);
        Assert.True(await usuarios.EhProfissionalAtivoAsync(prof, ct));
        Assert.Contains(listaCursos, c => c.Id == curso);
        Assert.True(cursoAtivo);
        Assert.Equal(4, indices.Count);
    }
}
