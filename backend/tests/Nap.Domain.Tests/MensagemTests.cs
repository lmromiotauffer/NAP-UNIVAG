using Nap.Domain.Comum;
using Nap.Domain.Comunicacoes;

namespace Nap.Domain.Tests;

public class MensagemTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Autor = Guid.CreateVersion7();
    private static readonly Guid Destinatario = Guid.CreateVersion7();
    private static readonly Guid Terceiro = Guid.CreateVersion7();

    private static Mensagem Rascunho(Guid? destinatario = null, string assunto = "Assunto", string corpo = "Corpo",
        Guid? aluno = null, Guid? curso = null, Guid? encaminhamento = null) =>
        Mensagem.CriarRascunho(Autor, TipoMensagem.Aviso, assunto, corpo, destinatario, curso, aluno, encaminhamento, Agora);

    private static Mensagem Enviada(string assunto = "Assunto")
    {
        var m = Rascunho(Destinatario, assunto, aluno: Guid.CreateVersion7(), curso: Guid.CreateVersion7(),
            encaminhamento: Guid.CreateVersion7());
        m.Enviar(Agora);
        return m;
    }

    [Fact]
    public void Rascunho_nao_gera_evento_e_enviar_gera()
    {
        var m = Rascunho(Destinatario);
        Assert.Equal(EstadoMensagem.Rascunho, m.Estado);
        Assert.Equal(m.Id, m.FioId);
        Assert.Empty(m.Eventos);

        m.Enviar(Agora);

        Assert.Equal(EstadoMensagem.Enviada, m.Estado);
        Assert.Equal(Agora, m.EnviadaEm);
        var evento = Assert.IsType<MensagemEnviada>(Assert.Single(m.Eventos));
        Assert.Equal(m.Id, evento.MensagemId);
        Assert.Equal(Autor, evento.RemetenteId);
        Assert.Equal(Destinatario, evento.DestinatarioId);
    }

    [Fact]
    public void Enviar_sem_destinatario_e_rejeitado()
    {
        var m = Rascunho();
        Assert.Throws<DomainException>(() => m.Enviar(Agora));
        Assert.Equal(EstadoMensagem.Rascunho, m.Estado);
    }

    [Fact]
    public void Enviada_e_imutavel()
    {
        var m = Enviada();
        Assert.Throws<DomainException>(() =>
            m.AtualizarRascunho(TipoMensagem.Aviso, "Outro", "Outro", Destinatario, null, null, null, Agora));
        Assert.Throws<DomainException>(() => m.Enviar(Agora));
    }

    [Fact]
    public void So_o_destinatario_marca_lida()
    {
        var m = Enviada();
        Assert.Throws<DomainException>(() => m.MarcarLida(Autor, Agora));
        Assert.Throws<DomainException>(() => m.MarcarLida(Terceiro, Agora));

        m.MarcarLida(Destinatario, Agora);
        m.MarcarLida(Destinatario, Agora.AddHours(1));

        Assert.Equal(Agora, m.LidaEm);
    }

    [Fact]
    public void Marcar_lida_em_rascunho_e_rejeitado() =>
        Assert.Throws<DomainException>(() => Rascunho(Destinatario).MarcarLida(Destinatario, Agora));

    [Fact]
    public void Rascunho_para_o_proprio_autor_e_rejeitado() =>
        Assert.Throws<DomainException>(() => Rascunho(Autor));

    [Fact]
    public void Arquivar_e_por_participante()
    {
        var m = Enviada();
        m.Arquivar(Autor, Agora);
        Assert.Equal(Agora, m.ArquivadaPeloAutorEm);
        Assert.Null(m.ArquivadaPeloDestinatarioEm);
        Assert.Throws<DomainException>(() => m.Arquivar(Terceiro, Agora));
        Assert.Throws<DomainException>(() => m.Desarquivar(Terceiro));

        m.Desarquivar(Autor);
        Assert.Null(m.ArquivadaPeloAutorEm);
        Assert.Throws<DomainException>(() => Rascunho(Destinatario).Arquivar(Autor, Agora));
    }

    [Fact]
    public void Resposta_vai_ao_outro_participante_com_mesmos_vinculos_e_fio()
    {
        var original = Enviada("Dúvida");

        var resposta = Mensagem.CriarResposta(original, Destinatario, "Resposta", Agora.AddHours(1));

        Assert.Equal(Autor, resposta.DestinatarioId);
        Assert.Equal(Destinatario, resposta.AutorId);
        Assert.Equal(original.Tipo, resposta.Tipo);
        Assert.Equal(original.AlunoId, resposta.AlunoId);
        Assert.Equal(original.CursoId, resposta.CursoId);
        Assert.Equal(original.EncaminhamentoId, resposta.EncaminhamentoId);
        Assert.Equal(original.FioId, resposta.FioId);
        Assert.Equal(original.Id, resposta.RespostaDeId);
        Assert.Equal("Re: Dúvida", resposta.Assunto);
        Assert.Equal(EstadoMensagem.Enviada, resposta.Estado);
        Assert.IsType<MensagemEnviada>(Assert.Single(resposta.Eventos));

        var daVolta = Mensagem.CriarResposta(original, Autor, "Tréplica", Agora);
        Assert.Equal(Destinatario, daVolta.DestinatarioId);

        Assert.Throws<DomainException>(() => Mensagem.CriarResposta(original, Terceiro, "x", Agora));
        Assert.Throws<DomainException>(() => Mensagem.CriarResposta(Rascunho(Destinatario), Autor, "x", Agora));
    }

    [Fact]
    public void Resposta_ao_envio_de_pei_mantem_a_versao()
    {
        var versao = Guid.CreateVersion7();
        var envio = Mensagem.CriarEnvioPei(Autor, Destinatario, "PEI publicado: Aluno (versão 1)", "Corpo", Guid.CreateVersion7(),
            Guid.CreateVersion7(), versao, Agora);

        var resposta = Mensagem.CriarResposta(envio, Destinatario, "Ciente", Agora.AddHours(1));

        Assert.Equal(versao, resposta.PeiVersaoId);
        Assert.Equal(TipoMensagem.EnvioPei, resposta.Tipo);
    }

    [Fact]
    public void Resposta_nao_duplica_prefixo_re()
    {
        var resposta = Mensagem.CriarResposta(Enviada("Re: Dúvida"), Destinatario, "Resposta", Agora);
        Assert.Equal("Re: Dúvida", resposta.Assunto);

        var longa = Mensagem.CriarResposta(Enviada(new string('a', 200)), Destinatario, "Resposta", Agora);
        Assert.Equal(200, longa.Assunto.Length);
        Assert.StartsWith("Re: ", longa.Assunto);

        var emoji = Mensagem.CriarResposta(Enviada(new string('a', 195) + "😀"), Destinatario, "Resposta", Agora);
        Assert.False(char.IsHighSurrogate(emoji.Assunto[^1]));
    }

    [Fact]
    public void Limites_de_assunto_e_corpo()
    {
        Assert.Throws<DomainException>(() => Rascunho(assunto: "ab"));
        Assert.Throws<DomainException>(() => Rascunho(assunto: new string('a', 201)));
        Assert.Throws<DomainException>(() => Rascunho(corpo: "  "));
        Assert.Throws<DomainException>(() => Rascunho(corpo: new string('a', 10001)));
        Rascunho(assunto: new string('a', 200), corpo: new string('a', 10000));

        var m = Rascunho(Destinatario);
        Assert.Throws<DomainException>(() =>
            m.AtualizarRascunho(TipoMensagem.Aviso, "ab", "Corpo", Destinatario, null, null, null, Agora));
        Assert.Equal("Assunto", m.Assunto);
    }
}
