using Nap.Domain.Agenda;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum;

namespace Nap.Domain.Tests;

public class AtendimentoTests
{
    private static readonly DateTimeOffset Agora = CompromissoTests.Agora;
    private static readonly Guid Autor = CompromissoTests.Autor;
    private static readonly DadosClinicos Dados = new("Demanda", null, null, null);

    private static Atendimento Realizado(Guid? profissionalId = null) => Atendimento.RegistrarAvulso(
        Guid.CreateVersion7(), profissionalId ?? Autor, CompromissoTests.Intervalo(7),
        TipoAtendimento.Psicopedagogico, EstadoAtendimento.Realizado, Dados, "Atendimento não agendado", Autor, Agora);

    [Fact]
    public void Registrar_de_compromisso_conclui_o_compromisso()
    {
        var c = CompromissoTests.Novo(alunoId: Guid.CreateVersion7());
        var a = Atendimento.RegistrarDeCompromisso(c, TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, Dados, Autor, Agora);

        Assert.Equal(EstadoCompromisso.Concluido, c.Estado);
        Assert.Equal(a.Id, c.RegistroVinculadoId);
        Assert.Equal(c.Id, a.CompromissoId);
        Assert.Equal(c.AlunoId, a.AlunoId);
        Assert.Equal(c.ProfissionalId, a.ProfissionalId);
        Assert.Equal(c.Intervalo, a.Intervalo);
    }

    [Fact]
    public void Registrar_de_compromisso_que_nao_e_atendimento_falha()
    {
        var c = CompromissoTests.Novo(TipoCompromisso.Bloqueio);
        Assert.Throws<DomainException>(() =>
            Atendimento.RegistrarDeCompromisso(c, TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, Dados, Autor, Agora));
        Assert.Equal(EstadoCompromisso.Agendado, c.Estado);
    }

    [Fact]
    public void Registro_invalido_nao_conclui_o_compromisso()
    {
        var c = CompromissoTests.Novo(alunoId: Guid.CreateVersion7());
        Assert.Throws<DomainException>(() =>
            Atendimento.RegistrarDeCompromisso(c, TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, null, Autor, Agora));
        Assert.Equal(EstadoCompromisso.Agendado, c.Estado);
    }

    [Fact]
    public void Ausencia_nao_exige_demanda_nem_notas()
    {
        var c = CompromissoTests.Novo(alunoId: Guid.CreateVersion7());
        var a = Atendimento.RegistrarDeCompromisso(c, TipoAtendimento.Acompanhamento, EstadoAtendimento.Ausente, null, Autor, Agora);
        Assert.Equal(EstadoAtendimento.Ausente, a.Estado);
        Assert.Null(a.Dados);
    }

    [Fact]
    public void Realizado_sem_demanda_falha()
    {
        Assert.Throws<DomainException>(() => Atendimento.RegistrarAvulso(
            Guid.CreateVersion7(), Autor, CompromissoTests.Intervalo(7), TipoAtendimento.Acompanhamento,
            EstadoAtendimento.Realizado, new DadosClinicos(" ", null, null, null), "Justificativa", Autor, Agora));
        Assert.Throws<DomainException>(() => Atendimento.RegistrarAvulso(
            Guid.CreateVersion7(), Autor, CompromissoTests.Intervalo(7), TipoAtendimento.Acompanhamento,
            EstadoAtendimento.Realizado, null, "Justificativa", Autor, Agora));
    }

    [Theory]
    [InlineData(EstadoAtendimento.Remarcado)]
    [InlineData(EstadoAtendimento.Cancelado)]
    public void Estado_inicial_so_realizado_ou_ausente(EstadoAtendimento estado) =>
        Assert.Throws<DomainException>(() => Atendimento.RegistrarAvulso(
            Guid.CreateVersion7(), Autor, CompromissoTests.Intervalo(7), TipoAtendimento.Acompanhamento,
            estado, Dados, "Justificativa", Autor, Agora));

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    public void Avulso_sem_justificativa_falha(string? justificativa) =>
        Assert.Throws<DomainException>(() => Atendimento.RegistrarAvulso(
            Guid.CreateVersion7(), Autor, CompromissoTests.Intervalo(7), TipoAtendimento.Acompanhamento,
            EstadoAtendimento.Realizado, Dados, justificativa!, Autor, Agora));

    [Fact]
    public void Registro_de_atendimento_futuro_falha()
    {
        var ex = Assert.Throws<DomainException>(() => Atendimento.RegistrarAvulso(
            Guid.CreateVersion7(), Autor, CompromissoTests.Intervalo(13), TipoAtendimento.Acompanhamento,
            EstadoAtendimento.Realizado, Dados, "Justificativa", Autor, Agora));
        Assert.Equal("Não é possível registrar atendimento que ainda não terminou", ex.Message);
    }

    [Fact]
    public void Correcao_por_terceiro_exige_justificativa_e_registra_historico()
    {
        var a = Realizado();
        var terceiro = Guid.CreateVersion7();
        Assert.Throws<DomainException>(() => a.Corrigir(EstadoAtendimento.Ausente, a.Tipo, a.Intervalo, null, null, terceiro, false, Agora));
        Assert.Empty(a.Correcoes);

        a.Corrigir(EstadoAtendimento.Ausente, a.Tipo, a.Intervalo, null, "Aluno não compareceu", terceiro, false, Agora);

        Assert.Equal(EstadoAtendimento.Ausente, a.Estado);
        var corr = Assert.Single(a.Correcoes);
        Assert.Equal(terceiro, corr.AutorId);
        Assert.Equal(EstadoAtendimento.Realizado, corr.EstadoAnterior);
        Assert.Equal(EstadoAtendimento.Ausente, corr.EstadoNovo);
        Assert.Equal("Aluno não compareceu", corr.Justificativa);
    }

    [Fact]
    public void Correcao_pelo_responsavel_dispensa_justificativa_mas_mantem_coerencia()
    {
        var a = Realizado();
        a.Corrigir(EstadoAtendimento.Cancelado, a.Tipo, a.Intervalo, null, null, Autor, true, Agora);
        Assert.Equal(EstadoAtendimento.Cancelado, a.Estado);
        Assert.Throws<DomainException>(() =>
            a.Corrigir(EstadoAtendimento.Realizado, a.Tipo, a.Intervalo, null, null, Autor, true, Agora));
    }

    [Fact]
    public void Correcao_preserva_snapshot_anterior_inclusive_dados()
    {
        var a = Atendimento.RegistrarAvulso(Guid.CreateVersion7(), Autor, CompromissoTests.Intervalo(7),
            TipoAtendimento.Psicopedagogico, EstadoAtendimento.Realizado,
            new DadosClinicos("Demanda X", "Nota privada", null, null), "Atendimento não agendado", Autor, Agora);
        var intervaloAntigo = a.Intervalo;

        a.Corrigir(EstadoAtendimento.Cancelado, TipoAtendimento.Devolutiva, CompromissoTests.Intervalo(8), null, null, Autor, true, Agora);

        var corr = Assert.Single(a.Correcoes);
        Assert.Equal("Demanda X", corr.DadosAnteriores!.DemandaPrincipal);
        Assert.Equal("Nota privada", corr.DadosAnteriores.ObservacoesPrivadas);
        Assert.Equal(TipoAtendimento.Psicopedagogico, corr.TipoAnterior);
        Assert.Equal(intervaloAntigo.Inicio, corr.InicioAnterior);
        Assert.Equal(intervaloAntigo.Fim, corr.FimAnterior);
        Assert.Equal(TipoAtendimento.Devolutiva, a.Tipo);
        Assert.Equal(CompromissoTests.Intervalo(8), a.Intervalo);
        Assert.Null(a.Dados);
    }

    [Fact]
    public void Correcao_com_fim_no_futuro_falha()
    {
        var a = Realizado();
        Assert.Throws<DomainException>(() =>
            a.Corrigir(EstadoAtendimento.Realizado, a.Tipo, CompromissoTests.Intervalo(13), Dados, null, Autor, true, Agora));
        Assert.Empty(a.Correcoes);
    }

    [Fact]
    public void Ausente_com_conteudo_armazena_como_enviado()
    {
        var d = new DadosClinicos(null, "Nota", "Rec", null);
        var a = Atendimento.RegistrarAvulso(Guid.CreateVersion7(), Autor, CompromissoTests.Intervalo(7),
            TipoAtendimento.Acompanhamento, EstadoAtendimento.Ausente, d, "Justificativa", Autor, Agora);
        Assert.Equal(d, a.Dados);
    }

    [Fact]
    public void Segundo_registro_de_compromisso_concluido_falha()
    {
        var c = CompromissoTests.Novo(alunoId: Guid.CreateVersion7());
        Atendimento.RegistrarDeCompromisso(c, TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, Dados, Autor, Agora);
        Assert.Throws<DomainException>(() =>
            Atendimento.RegistrarDeCompromisso(c, TipoAtendimento.Acompanhamento, EstadoAtendimento.Realizado, Dados, Autor, Agora));
    }
}
