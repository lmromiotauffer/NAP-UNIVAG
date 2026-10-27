using Nap.Application.Features.Producao;
using Nap.Application.Features.Producao.Queries;
using Nap.Domain.Agenda;
using Nap.Domain.Alunos;
using Nap.Domain.Atendimentos;
using Nap.Domain.Atividades;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Producao;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Consultas;

namespace Nap.Infrastructure.Tests;

public class ProducaoQueriesTests(SqlServerFixture fx)
{
    // Semanas de 2015, longe das datas usadas pelos outros testes (feriados são globais no banco compartilhado).
    private static readonly DateOnly Segunda = new(2015, 3, 2);
    private static readonly TimeSpan Fuso = TimeSpan.FromHours(-4); // America/Cuiaba
    private static readonly DateTimeOffset Agora = new(2015, 4, 1, 12, 0, 0, Fuso);

    private static DateTimeOffset Em(DateOnly dia, int hora, int minuto = 0) => new(dia.ToDateTime(new TimeOnly(hora, minuto)), Fuso);
    private static IntervaloHorario Intervalo(DateOnly dia, int h1, int m1, int h2, int m2) => IntervaloHorario.Criar(Em(dia, h1, m1), Em(dia, h2, m2));

    private static ObterProducaoHandler Handler(NapDbContext db, UsuarioAtualTeste usuario)
    {
        var relogio = new RelogioTeste(Agora);
        return new ObterProducaoHandler(new ObterProducaoValidator(), new ProducaoQueries(db, relogio), new UsuarioQueries(db), usuario, relogio);
    }

    private static Atendimento Atendimento(Guid aluno, Guid prof, IntervaloHorario i, EstadoAtendimento estado,
        TipoAtendimento tipo = TipoAtendimento.Acompanhamento) =>
        Domain.Atendimentos.Atendimento.RegistrarAvulso(aluno, prof, i, tipo, estado,
            estado == EstadoAtendimento.Realizado ? new DadosClinicos("Demanda", null, null, null) : null, "Registro sem agenda", prof, Agora);

    [Fact]
    public async Task Identidade_numa_semana_fechada_sem_pendencias()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = (await RoundTripTests.CriarAlunoAsync(fx, curso, prof)).Id;
        var (seg, ter, qua, qui, sex) = (Segunda, Segunda.AddDays(1), Segunda.AddDays(2), Segunda.AddDays(3), Segunda.AddDays(4));

        await using (var db = fx.CriarContexto())
        {
            // Escala seg–sex 08–12h (240 min; sexta "sem atendimento" conta) = 1200; bloqueio de 1 h na terça → 1140; feriado na sexta → 900.
            db.EscalasProfissional.Add(EscalaProfissional.Criar(prof, new DateOnly(2015, 1, 1), null,
                Enum.GetValues<DayOfWeek>().Where(d => d is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                    .Select(d => new FaixaEscala(d, new TimeOnly(8, 0), new TimeOnly(12, 0), d == DayOfWeek.Friday)).ToList()));
            db.Compromissos.Add(Compromisso.Criar(TipoCompromisso.Bloqueio, "Consulta médica", Intervalo(ter, 10, 0, 11, 0), prof, null, null, prof));
            var cancelado = Compromisso.Criar(TipoCompromisso.Bloqueio, "Cancelado", Intervalo(qua, 8, 0, 9, 0), prof, null, null, prof);
            cancelado.Cancelar("Não precisou");
            db.Compromissos.Add(cancelado);
            // Remarcado não desconta: só o substituto (13–14h, fora da faixa) vale.
            var remarcado = Compromisso.Criar(TipoCompromisso.Bloqueio, "Remarcado", Intervalo(qua, 10, 0, 11, 0), prof, null, null, prof);
            db.Compromissos.AddRange(remarcado, remarcado.Remarcar(Intervalo(qua, 13, 0, 14, 0), prof));
            db.Feriados.Add(Feriado.Criar(sex, "Feriado de teste"));

            db.Atendimentos.Add(Atendimento(aluno, prof, Intervalo(seg, 8, 0, 9, 0), EstadoAtendimento.Realizado));
            db.Atendimentos.Add(Atendimento(aluno, prof, Intervalo(seg, 11, 30, 12, 30), EstadoAtendimento.Realizado)); // 30 na escala + 30 extra
            db.Atendimentos.Add(Atendimento(aluno, prof, Intervalo(ter, 8, 0, 8, 50), EstadoAtendimento.Ausente));
            db.Atendimentos.Add(Atendimento(aluno, prof, Intervalo(qua, 9, 0, 10, 0), EstadoAtendimento.Realizado, TipoAtendimento.Reuniao));

            var reuniao = Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião de equipe", Intervalo(qui, 9, 0, 10, 0), prof, null, null, prof);
            reuniao.RegistrarRealizacao(Intervalo(qui, 9, 15, 10, 30), prof, Agora); // efetivo: 75 min
            db.Compromissos.Add(reuniao);
            // Na sexta (feriado) a atividade inteira vira tempo extra.
            db.AtividadesCurso.Add(AtividadeCurso.RegistrarAvulsa(prof, Intervalo(sex, 10, 0, 11, 0), curso, TipoAtividade.RodaConversa,
                "Roda de conversa", "Atividade sem agenda", prof, Agora));
            // Compromisso de atividade concluído pelo registro: conta só o registro (30 min), nunca o compromisso.
            var compromissoAtividade = Compromisso.Criar(TipoCompromisso.AtividadeCurso, "Oficina", Intervalo(qui, 11, 0, 11, 30), prof, null, null,
                prof);
            db.Compromissos.Add(compromissoAtividade);
            db.AtividadesCurso.Add(AtividadeCurso.RegistrarDeCompromisso(compromissoAtividade, Intervalo(qui, 11, 0, 11, 30), curso,
                TipoAtividade.RodaConversa, "Oficina", prof, Agora));

            var fechamento = FechamentoSemanal.Criar(prof, Segunda);
            fechamento.Fechar(prof, Agora, DateOnly.FromDateTime(Agora.DateTime));
            db.FechamentosSemanais.Add(fechamento);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var p = await Handler(leitura, new UsuarioAtualTeste(prof)).Handle(new ObterProducaoQuery(TipoPeriodo.Semana, qua, null, null), ct);

        var t = p.Tempo!;
        Assert.Equal(0, p.Pendentes);
        Assert.False(t.Inconsistente);
        Assert.Equal(t.Capacidade, t.ProducaoNaEscala + t.AusenciasNaEscala + t.Ociosidade);
        Assert.Equal((900, 60 + 30 + 60 + 75 + 30, 50, 0, 900 - 255 - 50, 30 + 60),
            (t.Capacidade, t.ProducaoNaEscala, t.AusenciasNaEscala, t.PendentesNaEscala, t.Ociosidade, t.TempoExtra));
        ValorProducaoDto Total(CategoriaProducao c) => p.Categorias.Single(x => x.Chave == c).Total;
        Assert.Equal(new ValorProducaoDto(3, 180), Total(CategoriaProducao.AtendimentosRealizados));
        Assert.Equal(new ValorProducaoDto(1, 50), Total(CategoriaProducao.Ausencias));
        Assert.Equal(new ValorProducaoDto(1, 75), Total(CategoriaProducao.ReunioesEquipe));
        Assert.Equal(new ValorProducaoDto(2, 90), Total(CategoriaProducao.AtividadesCursos));
        Assert.Equal(new ValorProducaoDto(null, 595), Total(CategoriaProducao.Ociosidade));
        Assert.Equal(new TaxaDto(1m / 3, 1, 3, null), p.TaxaAusencia);
        Assert.False(p.Periodo.Parcial);
    }

    [Fact]
    public async Task Fatos_incluem_ocorrencias_e_pendentes_que_entram_no_primeiro_dia_pela_virada()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var curso = await fx.CriarCursoAsync();
        var aluno = (await RoundTripTests.CriarAlunoAsync(fx, curso, prof)).Id;
        var semana = Segunda.AddDays(14);
        var domingo = semana.AddDays(-1);
        var virada = IntervaloHorario.Criar(Em(domingo, 23, 0), Em(semana, 1, 0));
        var antes = IntervaloHorario.Criar(Em(domingo, 20, 0), Em(domingo, 21, 0));

        await using (var db = fx.CriarContexto())
        {
            db.Atendimentos.Add(Atendimento(aluno, prof, virada, EstadoAtendimento.Realizado));
            db.Atendimentos.Add(Atendimento(aluno, prof, antes, EstadoAtendimento.Realizado)); // termina antes da semana: fora
            db.Compromissos.Add(Compromisso.Criar(TipoCompromisso.Atendimento, "Sem desfecho", virada, prof, aluno, null, prof));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var fatos = await new ProducaoQueries(leitura, new RelogioTeste(Agora)).CarregarFatosAsync([prof], semana, semana.AddDays(6), null, ct);

        Assert.Equal(virada.Inicio, Assert.Single(fatos.Ocorrencias).Inicio);
        Assert.Equal(virada.Inicio, Assert.Single(fatos.Pendentes).Inicio);
    }

    [Fact]
    public async Task Profissional_desativado_continua_na_producao_passada_sem_capacidade_apos_desativar()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: $"Desativado {Guid.NewGuid():N}");
        var curso = await fx.CriarCursoAsync();
        var aluno = (await RoundTripTests.CriarAlunoAsync(fx, curso, prof)).Id;
        var segunda = Segunda.AddDays(14);
        await using (var db = fx.CriarContexto())
        {
            db.Atendimentos.Add(Atendimento(aluno, prof, Intervalo(segunda, 8, 0, 9, 0), EstadoAtendimento.Realizado));
            db.Atendimentos.Add(Atendimento(aluno, prof, Intervalo(segunda.AddDays(3), 8, 0, 9, 0), EstadoAtendimento.Realizado));
            await db.SaveChangesAsync(ct);
            // Desativado na quarta: sem escala própria vale a padrão (600 min/dia), então só seg e ter têm capacidade.
            var u = await db.Users.FindAsync([prof], ct);
            (u!.Ativo, u.DesativadoEm) = (false, segunda.AddDays(2));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var admin = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.Administrador);
        var equipe = await Handler(leitura, admin).Handle(new ObterProducaoQuery(TipoPeriodo.Semana, segunda, null, null), ct);
        var filtrado = await Handler(leitura, admin).Handle(new ObterProducaoQuery(TipoPeriodo.Semana, segunda, prof, null), ct);

        var linha = Assert.Single(equipe.PorProfissional!, l => l.ProfissionalId == prof);
        Assert.Equal(new ValorProducaoDto(2, 120), linha.Categorias.Single(c => c.Chave == CategoriaProducao.AtendimentosRealizados).Total);
        Assert.Equal(1200, linha.Tempo!.Capacidade);
        Assert.Equal(equipe.PorProfissional!.Sum(l => l.Tempo!.Capacidade), equipe.Tempo!.Capacidade);
        Assert.True(equipe.Categorias.Single(c => c.Chave == CategoriaProducao.AtendimentosRealizados).Total.Ocorrencias >= 2);
        Assert.Equal(new ValorProducaoDto(2, 120), filtrado.Categorias.Single(c => c.Chave == CategoriaProducao.AtendimentosRealizados).Total);
        // O atendimento de quinta (após desativar) conta na categoria, mas sem faixa vira tempo extra.
        var t = filtrado.Tempo!;
        Assert.Equal((1200, 60, 1200 - 60, 60), (t.Capacidade, t.ProducaoNaEscala, t.Ociosidade, t.TempoExtra));
    }

    [Fact]
    public async Task Profissional_que_saiu_da_equipe_mantem_a_producao_passada()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync(nome: $"Saiu da equipe {Guid.NewGuid():N}");
        var curso = await fx.CriarCursoAsync();
        var aluno = (await RoundTripTests.CriarAlunoAsync(fx, curso, prof)).Id;
        var segunda = Segunda.AddDays(21);
        await using (var db = fx.CriarContexto())
        {
            db.Atendimentos.Add(Atendimento(aluno, prof, Intervalo(segunda, 8, 0, 9, 0), EstadoAtendimento.Realizado));
            await db.SaveChangesAsync(ct);
            var u = await db.Users.FindAsync([prof], ct);
            u!.Perfil = Perfis.CoordenadorCurso;
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var admin = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.Administrador);
        var equipe = await Handler(leitura, admin).Handle(new ObterProducaoQuery(TipoPeriodo.Semana, segunda, null, null), ct);
        // Duas semanas depois: a consulta também carrega a semana anterior, que também fica sem fatos.
        var semFatos = await Handler(leitura, admin).Handle(new ObterProducaoQuery(TipoPeriodo.Semana, segunda.AddDays(14), null, null), ct);

        var linha = Assert.Single(equipe.PorProfissional!, l => l.ProfissionalId == prof);
        Assert.Equal(new ValorProducaoDto(1, 60), linha.Categorias.Single(c => c.Chave == CategoriaProducao.AtendimentosRealizados).Total);
        // Sem fatos no período, o perfil atual decide: fora da equipe NAP, fora da produção.
        Assert.DoesNotContain(semFatos.PorProfissional!, l => l.ProfissionalId == prof);
    }

    [Fact]
    public async Task Filtro_de_curso_exclui_institucionais_e_outros_cursos_sem_consulta_por_dia()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var (curso, outroCurso) = (await fx.CriarCursoAsync(), await fx.CriarCursoAsync());
        var aluno = (await RoundTripTests.CriarAlunoAsync(fx, curso, prof)).Id;
        var alunoDeOutro = (await RoundTripTests.CriarAlunoAsync(fx, outroCurso, prof)).Id;
        var dia = Segunda.AddDays(8);
        await using (var db = fx.CriarContexto())
        {
            db.Atendimentos.Add(Atendimento(aluno, prof, Intervalo(dia, 8, 0, 9, 0), EstadoAtendimento.Realizado));
            db.Atendimentos.Add(Atendimento(alunoDeOutro, prof, Intervalo(dia, 9, 0, 10, 0), EstadoAtendimento.Realizado));
            foreach (var (c, h) in new (Guid?, int)[] { (curso, 10), (null, 11), (outroCurso, 12) })
                db.AtividadesCurso.Add(AtividadeCurso.RegistrarAvulsa(prof, Intervalo(dia, h, 0, h + 1, 0), c, TipoAtividade.RodaConversa,
                    "Roda de conversa", "Atividade sem agenda", prof, Agora));
            await db.SaveChangesAsync(ct);
        }

        var comandos = 0;
        await using var leitura = fx.CriarContexto(l => { if (l.Contains("Executed DbCommand")) Interlocked.Increment(ref comandos); });
        var coordenador = new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorCurso, curso);
        var p = await Handler(leitura, coordenador).Handle(new ObterProducaoQuery(TipoPeriodo.Ano, dia, null, null), ct);

        Assert.Null(p.Tempo);
        Assert.Equal(new ValorProducaoDto(1, 60), p.Categorias.Single(c => c.Chave == CategoriaProducao.AtendimentosRealizados).Total);
        Assert.Equal(new ValorProducaoDto(1, 60), p.Categorias.Single(c => c.Chave == CategoriaProducao.AtividadesCursos).Total);
        Assert.InRange(comandos, 1, 10); // dois anos de dias, carregados em lote
    }

    [Fact]
    public async Task Filtro_de_curso_usa_o_vinculo_vigente_na_data_local_do_atendimento()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var (cursoA, cursoB) = (await fx.CriarCursoAsync(), await fx.CriarCursoAsync());
        var (antes, transferencia) = (Segunda.AddDays(28), Segunda.AddDays(29));
        var aluno = Aluno.Criar("Aluno transferido", Matricula.Criar(Guid.NewGuid().ToString("N")[..20]), cursoA, 3,
            new DateOnly(2015, 1, 1), prof, null, null, prof, Agora);
        await using (var db = fx.CriarContexto())
        {
            aluno.TransferirCurso(cursoB, transferencia, "Transferência de curso", prof, Agora);
            db.Alunos.Add(aluno);
            // 21h local da véspera = 01h UTC do dia da transferência: ainda é do curso A.
            db.Atendimentos.Add(Atendimento(aluno.Id, prof, Intervalo(antes, 21, 0, 22, 0), EstadoAtendimento.Realizado));
            db.Atendimentos.Add(Atendimento(aluno.Id, prof, Intervalo(transferencia, 8, 0, 8, 30), EstadoAtendimento.Realizado));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        async Task<ValorProducaoDto> Realizados(Guid curso) =>
            (await Handler(leitura, new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorCurso, curso))
                .Handle(new ObterProducaoQuery(TipoPeriodo.Ano, antes, null, null), ct))
            .Categorias.Single(c => c.Chave == CategoriaProducao.AtendimentosRealizados).Total;

        Assert.Equal(new ValorProducaoDto(1, 60), await Realizados(cursoA));
        Assert.Equal(new ValorProducaoDto(1, 30), await Realizados(cursoB));
    }

    [Fact]
    public async Task Profissional_criado_no_meio_do_periodo_nao_tem_capacidade_antes_de_ativo_desde()
    {
        var ct = TestContext.Current.CancellationToken;
        var prof = await fx.CriarProfissionalAsync();
        var segunda = Segunda.AddDays(21);
        await using (var db = fx.CriarContexto())
        {
            // Criado na quarta: sem escala própria vale a padrão (600 min/dia), então só qua, qui e sex têm capacidade.
            var u = await db.Users.FindAsync([prof], ct);
            u!.AtivoDesde = segunda.AddDays(2);
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var p = await Handler(leitura, new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorNap))
            .Handle(new ObterProducaoQuery(TipoPeriodo.Semana, segunda, prof, null), ct);

        Assert.Equal(1800, p.Tempo!.Capacidade);
    }

    [Fact]
    public async Task Administrador_sem_escala_nao_tem_capacidade_mas_as_ocorrencias_contam()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await fx.CriarProfissionalAsync(Perfis.Administrador);
        var curso = await fx.CriarCursoAsync();
        var aluno = (await RoundTripTests.CriarAlunoAsync(fx, curso, admin)).Id;
        var segunda = Segunda.AddDays(21);
        await using (var db = fx.CriarContexto())
        {
            db.Atendimentos.Add(Atendimento(aluno, admin, Intervalo(segunda, 8, 0, 9, 0), EstadoAtendimento.Realizado));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        var p = await Handler(leitura, new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorNap))
            .Handle(new ObterProducaoQuery(TipoPeriodo.Semana, segunda, admin, null), ct);

        Assert.Equal(new ValorProducaoDto(1, 60), p.Categorias.Single(c => c.Chave == CategoriaProducao.AtendimentosRealizados).Total);
        var t = p.Tempo!;
        Assert.Equal((0, 0, 0, 60), (t.Capacidade, t.ProducaoNaEscala, t.Ociosidade, t.TempoExtra));
    }

    [Fact]
    public async Task Ausencias_no_filtro_de_curso_usam_a_escala_real()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await fx.CriarProfissionalAsync(Perfis.Administrador);
        var curso = await fx.CriarCursoAsync();
        var aluno = (await RoundTripTests.CriarAlunoAsync(fx, curso, admin)).Id;
        var segunda = Segunda.AddDays(28);
        await using (var db = fx.CriarContexto())
        {
            db.Atendimentos.Add(Atendimento(aluno, admin, Intervalo(segunda, 8, 0, 9, 0), EstadoAtendimento.Ausente));
            await db.SaveChangesAsync(ct);
        }

        await using var leitura = fx.CriarContexto();
        async Task<ValorProducaoDto> Ausencias(Guid? cursoId) =>
            (await Handler(leitura, new UsuarioAtualTeste(Guid.NewGuid(), Perfis.CoordenadorNap))
                .Handle(new ObterProducaoQuery(TipoPeriodo.Semana, segunda, admin, cursoId), ct))
            .Categorias.Single(c => c.Chave == CategoriaProducao.Ausencias).Total;

        // Administrador sem escala própria não tem escala: a falta conta 0 min com ou sem o recorte (antes, o recorte caía na padrão).
        Assert.Equal(new ValorProducaoDto(1, 0), await Ausencias(null));
        Assert.Equal(new ValorProducaoDto(1, 0), await Ausencias(curso));
    }
}
