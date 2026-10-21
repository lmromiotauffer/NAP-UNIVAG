using Nap.Domain.Agenda;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Producao;

namespace Nap.Domain.Tests;

public class CompromissoTests
{
    internal static readonly DateTimeOffset Agora = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);
    internal static readonly Guid Autor = Guid.CreateVersion7();

    internal static IntervaloHorario Intervalo(int h = 7) =>
        IntervaloHorario.Criar(new(2026, 3, 2, h, 0, 0, TimeSpan.Zero), new(2026, 3, 2, h + 1, 0, 0, TimeSpan.Zero));

    internal static Compromisso Novo(TipoCompromisso tipo = TipoCompromisso.Atendimento, Guid? alunoId = null) =>
        Compromisso.Criar(tipo, "Título", Intervalo(), Guid.CreateVersion7(), alunoId, null, Autor, Agora);

    [Fact]
    public void Lembrete_nao_ocupa_horario()
    {
        Assert.False(TipoCompromisso.Lembrete.OcupaHorario());
        Assert.True(TipoCompromisso.Bloqueio.OcupaHorario());
        Assert.True(TipoCompromisso.Atendimento.OcupaHorario());
    }

    [Fact]
    public void Atendimento_na_agenda_exige_aluno() =>
        Assert.Throws<DomainException>(() => Novo(TipoCompromisso.Atendimento, null));

    [Theory]
    [InlineData(TipoCompromisso.Bloqueio)]
    [InlineData(TipoCompromisso.Lembrete)]
    public void Bloqueio_nao_aceita_aluno(TipoCompromisso tipo) =>
        Assert.Throws<DomainException>(() => Novo(tipo, Guid.CreateVersion7()));

    [Fact]
    public void Titulo_vazio_ou_longo_falha()
    {
        Assert.Throws<DomainException>(() => Compromisso.Criar(TipoCompromisso.Bloqueio, " ", Intervalo(), Guid.CreateVersion7(), null, null, Autor, Agora));
        Assert.Throws<DomainException>(() => Compromisso.Criar(TipoCompromisso.Bloqueio, new string('x', 121), Intervalo(), Guid.CreateVersion7(), null, null, Autor, Agora));
    }

    [Fact]
    public void GrupoId_e_definido_na_criacao_e_mantido_ao_remarcar()
    {
        var grupo = Guid.CreateVersion7();
        var original = Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Título", Intervalo(), Guid.CreateVersion7(), null, null, Autor, Agora, grupo);

        Assert.Equal(grupo, original.GrupoId);
        Assert.Equal(grupo, original.Remarcar(Intervalo(9), Autor, Agora).GrupoId);
        Assert.Null(Compromisso.Criar(TipoCompromisso.Bloqueio, "Título", Intervalo(), Guid.CreateVersion7(), null, null, Autor, Agora).GrupoId);
    }

    [Fact]
    public void Remarcar_preserva_vinculo_entre_original_e_novo()
    {
        var original = Novo(alunoId: Guid.CreateVersion7());
        var novo = original.Remarcar(Intervalo(9), Autor, Agora);

        Assert.Equal(original.Id, novo.RemarcadoDeId);
        Assert.Equal(novo.Id, original.RemarcadoParaId);
        Assert.Equal(EstadoCompromisso.Remarcado, original.Estado);
        Assert.Equal(EstadoCompromisso.Agendado, novo.Estado);
        Assert.Equal(original.AlunoId, novo.AlunoId);
    }

    [Fact]
    public void Compromisso_cancelado_nao_pode_ser_concluido()
    {
        var c = Novo(alunoId: Guid.CreateVersion7());
        c.Cancelar("Aluno desistiu", Autor, Agora);
        Assert.Equal("Aluno desistiu", c.MotivoCancelamento);
        Assert.Throws<DomainException>(() => c.Concluir(Guid.CreateVersion7()));
        Assert.Throws<DomainException>(() => c.Remarcar(Intervalo(9), Autor, Agora));
        Assert.Throws<DomainException>(() => c.Atualizar("Novo", null));
    }

    [Fact]
    public void Cancelar_exige_motivo() =>
        Assert.Throws<DomainException>(() => Novo(alunoId: Guid.CreateVersion7()).Cancelar(" ", Autor, Agora));

    [Fact]
    public void Escala_padrao_sexta_sem_atendimento()
    {
        var sexta = Assert.Single(EscalaPadrao.Faixas, f => f.Dia == DayOfWeek.Friday);
        Assert.True(sexta.SemAtendimento);
        Assert.Equal(4, EscalaPadrao.Faixas.Count(f => !f.SemAtendimento));
        Assert.All(EscalaPadrao.Faixas, f =>
        {
            Assert.Equal(new TimeOnly(7, 0), f.Inicio);
            Assert.Equal(new TimeOnly(17, 0), f.Fim);
        });
    }

    [Theory]
    [InlineData("ab")]
    [InlineData(null)]
    public void Cancelar_motivo_curto_falha(string? motivo) =>
        Assert.Throws<DomainException>(() => Novo(alunoId: Guid.CreateVersion7()).Cancelar(motivo!, Autor, Agora));

    [Fact]
    public void Cancelar_motivo_longo_falha() =>
        Assert.Throws<DomainException>(() => Novo(alunoId: Guid.CreateVersion7()).Cancelar(new string('x', 501), Autor, Agora));

    [Fact]
    public void Realizacao_so_depois_do_termino()
    {
        var futuro = Compromisso.Criar(TipoCompromisso.ReuniaoEquipe, "Reunião", Intervalo(12), Guid.CreateVersion7(), null, null, Autor, Agora);
        var ex = Assert.Throws<DomainException>(() => futuro.RegistrarRealizacao(Intervalo(10), Autor, Agora));
        Assert.Equal("Só é possível registrar a realização depois do término.", ex.Message);

        var c = Novo(TipoCompromisso.ReuniaoEquipe);
        Assert.Throws<DomainException>(() => c.RegistrarRealizacao(Intervalo(12), Autor, Agora));
        Assert.Equal(c.Intervalo, c.IntervaloEfetivo);

        var efetivo = IntervaloHorario.Criar(new(2026, 3, 2, 7, 15, 0, TimeSpan.Zero), new(2026, 3, 2, 8, 30, 0, TimeSpan.Zero));
        c.RegistrarRealizacao(efetivo, Autor, Agora);
        Assert.Equal(EstadoCompromisso.Concluido, c.Estado);
        Assert.Equal(efetivo, c.IntervaloEfetivo);
        Assert.Equal(efetivo.Inicio, c.RealizacaoInicio);
        Assert.Equal(efetivo.Fim, c.RealizacaoFim);
        Assert.Equal(Autor, c.RealizadoPorId);
        Assert.Throws<DomainException>(() => c.RegistrarRealizacao(efetivo, Autor, Agora));
    }

    [Fact]
    public void Correcao_de_realizacao_so_em_compromisso_realizado()
    {
        var agendado = Novo(TipoCompromisso.ReuniaoEquipe);
        Assert.Throws<DomainException>(() => agendado.CorrigirRealizacao(Intervalo(8), "Horário errado", Autor, Agora));

        var concluidoPorRegistro = Novo(alunoId: Guid.CreateVersion7());
        concluidoPorRegistro.Concluir(Guid.CreateVersion7());
        Assert.Throws<DomainException>(() => concluidoPorRegistro.CorrigirRealizacao(Intervalo(8), "Horário errado", Autor, Agora));
        Assert.Empty(agendado.CorrecoesRealizacao);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   abc  ")]
    [InlineData(null)]
    public void Correcao_de_realizacao_exige_justificativa(string? justificativa)
    {
        var c = Novo(TipoCompromisso.ReuniaoEquipe);
        c.RegistrarRealizacao(Intervalo(), Autor, Agora);
        Assert.Throws<DomainException>(() => c.CorrigirRealizacao(Intervalo(8), justificativa!, Autor, Agora));
        Assert.Throws<DomainException>(() => c.CorrigirRealizacao(Intervalo(8), new string('x', 501), Autor, Agora));
        Assert.Equal(Intervalo(), c.IntervaloEfetivo);
    }

    [Fact]
    public void Correcao_de_realizacao_guarda_historico_e_exige_termino()
    {
        var c = Novo(TipoCompromisso.ReuniaoEquipe);
        c.RegistrarRealizacao(Intervalo(), Autor, Agora);
        Assert.Throws<DomainException>(() => c.CorrigirRealizacao(Intervalo(12), "Horário errado", Autor, Agora));

        var corretor = Guid.CreateVersion7();
        c.CorrigirRealizacao(Intervalo(9), "  Horário errado  ", corretor, Agora);
        c.CorrigirRealizacao(Intervalo(10), "De novo errado", corretor, Agora.AddMinutes(1));

        Assert.Equal(EstadoCompromisso.Concluido, c.Estado);
        Assert.Equal(Intervalo(10), c.IntervaloEfetivo);
        Assert.Equal(Autor, c.RealizadoPorId);
        var historico = c.CorrecoesRealizacao.ToList();
        Assert.Equal(2, historico.Count);
        Assert.Equal((corretor, Agora, "Horário errado", Intervalo().Inicio, Intervalo().Fim),
            (historico[0].AutorId, historico[0].Em, historico[0].Justificativa, historico[0].InicioAnterior, historico[0].FimAnterior));
        Assert.Equal(Intervalo(9).Inicio, historico[1].InicioAnterior);
    }

    [Theory]
    [InlineData(TipoCompromisso.ReuniaoEquipe, true)]
    [InlineData(TipoCompromisso.ReuniaoCoordenacaoGestao, true)]
    [InlineData(TipoCompromisso.ElaboracaoPei, true)]
    [InlineData(TipoCompromisso.TarefaAdministrativa, true)]
    [InlineData(TipoCompromisso.EncaminhamentoDevolutiva, true)]
    [InlineData(TipoCompromisso.AtividadeCurso, false)]
    [InlineData(TipoCompromisso.Bloqueio, false)]
    [InlineData(TipoCompromisso.Lembrete, false)]
    public void Realizacao_so_para_os_tipos_sem_registro_proprio(TipoCompromisso tipo, bool exige)
    {
        Assert.Equal(exige, tipo.ExigeRealizacao());
        var c = Novo(tipo);
        if (exige) c.RegistrarRealizacao(Intervalo(), Autor, Agora);
        else Assert.Throws<DomainException>(() => c.RegistrarRealizacao(Intervalo(), Autor, Agora));
    }

    [Fact]
    public void Atendimento_nao_tem_realizacao()
    {
        Assert.False(TipoCompromisso.Atendimento.ExigeRealizacao());
        Assert.Throws<DomainException>(() => Novo(alunoId: Guid.CreateVersion7()).RegistrarRealizacao(Intervalo(), Autor, Agora));
    }

    [Fact]
    public void Categoria_de_cada_tipo()
    {
        Assert.Equal(CategoriaProducao.ReunioesEquipe, TipoCompromisso.ReuniaoEquipe.CategoriaDe());
        Assert.Equal(CategoriaProducao.ReunioesCoordenacao, TipoCompromisso.ReuniaoCoordenacaoGestao.CategoriaDe());
        Assert.Equal(CategoriaProducao.ElaboracaoPei, TipoCompromisso.ElaboracaoPei.CategoriaDe());
        Assert.Equal(CategoriaProducao.Administrativo, TipoCompromisso.TarefaAdministrativa.CategoriaDe());
        Assert.Equal(CategoriaProducao.Administrativo, TipoCompromisso.EncaminhamentoDevolutiva.CategoriaDe());
        Assert.Equal(CategoriaProducao.AtividadesCursos, TipoCompromisso.AtividadeCurso.CategoriaDe());
        Assert.Null(TipoCompromisso.Atendimento.CategoriaDe());
        Assert.Null(TipoCompromisso.Bloqueio.CategoriaDe());
        Assert.Null(TipoCompromisso.Lembrete.CategoriaDe());
    }

    [Fact]
    public void Cancelar_depois_do_termino_e_permitido()
    {
        var c = Novo(TipoCompromisso.ReuniaoEquipe);
        c.Cancelar("Não realizada", Autor, Agora.AddDays(3));
        Assert.Equal(EstadoCompromisso.Cancelado, c.Estado);
    }
}
