using Nap.Domain.Comum;
using Nap.Domain.Encaminhamentos;

namespace Nap.Domain.Tests;

public class EncaminhamentoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2026, 3, 20);
    private static readonly DateOnly Data = new(2026, 3, 10);
    private static readonly Guid Aluno = Guid.CreateVersion7();
    private static readonly Guid Curso = Guid.CreateVersion7();
    private static readonly Guid Solicitante = Guid.CreateVersion7();
    private static readonly Guid Autor = Guid.CreateVersion7();

    private static Encaminhamento Novo(Especialidade e = Especialidade.Psicologia,
        DestinoEncaminhamento d = DestinoEncaminhamento.Sip, DateOnly? data = null) =>
        Encaminhamento.Criar(Aluno, Curso, Solicitante, e, d, data ?? Data, "Motivo restrito", "Resumo", "Obs", Hoje, Agora);

    private static Encaminhamento EmAtendimento()
    {
        var e = Novo();
        e.RegistrarRetorno(AcaoRetorno.Acolhimento, new DateOnly(2026, 3, 11), null, Autor, Hoje, Agora);
        e.RegistrarRetorno(AcaoRetorno.InicioAtendimento, new DateOnly(2026, 3, 12), null, Autor, Hoje, Agora);
        return e;
    }

    [Fact]
    public void Criar_registra_evento_e_comeca_encaminhado()
    {
        var e = Novo(Especialidade.Psiquiatria, DestinoEncaminhamento.ClinicaEscola);

        Assert.Equal(EstadoEncaminhamento.Encaminhado, e.Estado);
        var ev = Assert.IsType<EncaminhamentoCriado>(Assert.Single(e.Eventos));
        Assert.Equal(DestinoEncaminhamento.ClinicaEscola, ev.Destino);
        Assert.Equal(Curso, ev.CursoId);
        Assert.Equal((e.Id, Aluno, Solicitante), (ev.EncaminhamentoId, ev.AlunoId, ev.SolicitanteId));
        e.LimparEventos();
        Assert.Empty(e.Eventos);
    }

    [Fact]
    public void Especialidade_coordenacao_exige_destino_coordenacao()
    {
        Assert.Throws<DomainException>(() => Novo(Especialidade.CoordenacaoCurso, DestinoEncaminhamento.Sip));
        Assert.Throws<DomainException>(() => Novo(Especialidade.Psicologia, DestinoEncaminhamento.CoordenacaoCurso));
        Assert.Equal(DestinoEncaminhamento.CoordenacaoCurso,
            Novo(Especialidade.CoordenacaoCurso, DestinoEncaminhamento.CoordenacaoCurso).Destino);
    }

    [Fact]
    public void Data_futura_e_rejeitada()
    {
        Assert.Throws<DomainException>(() => Novo(data: Hoje.AddDays(1)));
        var e = Novo();
        Assert.Throws<DomainException>(() =>
            e.RegistrarRetorno(AcaoRetorno.Acolhimento, Hoje.AddDays(1), null, Autor, Hoje, Agora));
    }

    [Fact]
    public void Textos_respeitam_limites()
    {
        Assert.Throws<DomainException>(() => Encaminhamento.Criar(Aluno, Curso, Solicitante, Especialidade.Psicologia,
            DestinoEncaminhamento.Sip, Data, "ab", "Resumo", null, Hoje, Agora));
        Assert.Throws<DomainException>(() => Encaminhamento.Criar(Aluno, Curso, Solicitante, Especialidade.Psicologia,
            DestinoEncaminhamento.Sip, Data, "Motivo", new string('x', 2001), null, Hoje, Agora));
        Assert.Throws<DomainException>(() => Encaminhamento.Criar(Aluno, Curso, Solicitante, Especialidade.Psicologia,
            DestinoEncaminhamento.Sip, Data, "Motivo", "Resumo", new string('x', 4001), Hoje, Agora));
    }

    [Fact]
    public void Fluxo_completo_em_ordem()
    {
        var e = Novo();
        e.RegistrarRetorno(AcaoRetorno.Acolhimento, new DateOnly(2026, 3, 11), null, Autor, Hoje, Agora);
        e.RegistrarRetorno(AcaoRetorno.InicioAtendimento, new DateOnly(2026, 3, 12), null, Autor, Hoje, Agora);
        e.RegistrarRetorno(AcaoRetorno.Evolucao, null, "Evolução da sessão", Autor, Hoje, Agora);
        e.RegistrarRetorno(AcaoRetorno.Alta, new DateOnly(2026, 3, 18), null, Autor, Hoje, Agora);

        Assert.Equal(EstadoEncaminhamento.Alta, e.Estado);
        Assert.Equal(4, e.Evolucoes.Count);
        Assert.Equal(new DateOnly(2026, 3, 11), e.DataAcolhimento);
        Assert.Equal(new DateOnly(2026, 3, 12), e.DataPrimeiroAtendimento);
        Assert.Equal(new DateOnly(2026, 3, 18), e.DataEncerramento);
        Assert.Equal(Agora, e.AtualizadoEm);
    }

    [Fact]
    public void Acolhimento_antes_do_encaminhamento_e_rejeitado()
    {
        var e = Novo();
        Assert.Throws<DomainException>(() =>
            e.RegistrarRetorno(AcaoRetorno.Acolhimento, Data.AddDays(-1), null, Autor, Hoje, Agora));
    }

    [Fact]
    public void Alta_antes_do_primeiro_atendimento_e_rejeitada()
    {
        var e = EmAtendimento();
        Assert.Throws<DomainException>(() =>
            e.RegistrarRetorno(AcaoRetorno.Alta, new DateOnly(2026, 3, 11), null, Autor, Hoje, Agora));
    }

    [Fact]
    public void Inicio_sem_acolhimento_e_rejeitado()
    {
        var e = Novo();
        Assert.Throws<DomainException>(() =>
            e.RegistrarRetorno(AcaoRetorno.InicioAtendimento, Data, null, Autor, Hoje, Agora));
    }

    [Fact]
    public void Evolucao_exige_texto_e_nao_muda_estado()
    {
        var e = EmAtendimento();
        Assert.Throws<DomainException>(() => e.RegistrarRetorno(AcaoRetorno.Evolucao, null, " ", Autor, Hoje, Agora));

        e.RegistrarRetorno(AcaoRetorno.Evolucao, null, "Sessão realizada", Autor, Hoje, Agora);
        Assert.Equal(EstadoEncaminhamento.EmAtendimento, e.Estado);
        var ultima = e.Evolucoes.Last();
        Assert.Equal((EstadoEncaminhamento.EmAtendimento, EstadoEncaminhamento.EmAtendimento),
            (ultima.EstadoAnterior, ultima.EstadoNovo));
    }

    [Fact]
    public void Desistencia_e_cancelamento_exigem_justificativa()
    {
        var e = Novo();
        Assert.Throws<DomainException>(() => e.RegistrarRetorno(AcaoRetorno.Desistencia, Data, null, Autor, Hoje, Agora));
        Assert.Throws<DomainException>(() => e.RegistrarRetorno(AcaoRetorno.Desistencia, Data, "ab", Autor, Hoje, Agora));
        Assert.Throws<DomainException>(() => e.Cancelar(" ", Autor, Hoje, Agora));
        Assert.Throws<DomainException>(() => e.RegistrarRetorno(AcaoRetorno.Cancelamento, null, "Motivo", Autor, Hoje, Agora));

        e.RegistrarRetorno(AcaoRetorno.Desistencia, Data, "Aluno desistiu", Autor, Hoje, Agora);
        Assert.Equal(EstadoEncaminhamento.Desistencia, e.Estado);
        Assert.Equal(Data, e.DataEncerramento);

        var c = Novo();
        c.Cancelar("Encaminhamento duplicado", Autor, Hoje, Agora);
        Assert.Equal(EstadoEncaminhamento.Cancelado, c.Estado);
        Assert.Equal(Hoje, c.DataEncerramento);
        Assert.Equal(AcaoRetorno.Cancelamento, Assert.Single(c.Evolucoes).Acao);
    }

    [Theory]
    [InlineData(EstadoEncaminhamento.Alta)]
    [InlineData(EstadoEncaminhamento.Desistencia)]
    [InlineData(EstadoEncaminhamento.Cancelado)]
    public void Estado_terminal_e_imutavel(EstadoEncaminhamento terminal)
    {
        var e = EmAtendimento();
        switch (terminal)
        {
            case EstadoEncaminhamento.Alta: e.RegistrarRetorno(AcaoRetorno.Alta, Hoje, null, Autor, Hoje, Agora); break;
            case EstadoEncaminhamento.Desistencia: e.RegistrarRetorno(AcaoRetorno.Desistencia, Hoje, "Desistiu", Autor, Hoje, Agora); break;
            default: e.Cancelar("Cancelado", Autor, Hoje, Agora); break;
        }

        Assert.Throws<DomainException>(() => e.RegistrarRetorno(AcaoRetorno.Evolucao, null, "Texto", Autor, Hoje, Agora));
        Assert.Throws<DomainException>(() => e.RegistrarRetorno(AcaoRetorno.Alta, Hoje, null, Autor, Hoje, Agora));
        Assert.Throws<DomainException>(() => e.Cancelar("Outro", Autor, Hoje, Agora));
    }

    [Fact]
    public void Cada_retorno_registra_evento_com_solicitante_e_autor()
    {
        var e = Novo();
        e.LimparEventos();
        e.RegistrarRetorno(AcaoRetorno.Acolhimento, Data, null, Autor, Hoje, Agora);
        e.Cancelar("Não é mais necessário", Autor, Hoje, Agora);

        var eventos = e.Eventos.Cast<RetornoEncaminhamentoRegistrado>().ToList();
        Assert.Equal(2, eventos.Count);
        Assert.All(eventos, ev => Assert.Equal((Solicitante, Autor, Aluno, e.Id), (ev.SolicitanteId, ev.AutorId, ev.AlunoId, ev.EncaminhamentoId)));
        Assert.Equal((AcaoRetorno.Acolhimento, EstadoEncaminhamento.Acolhido), (eventos[0].Acao, eventos[0].Estado));
        Assert.Equal((AcaoRetorno.Cancelamento, EstadoEncaminhamento.Cancelado), (eventos[1].Acao, eventos[1].Estado));
    }
}
