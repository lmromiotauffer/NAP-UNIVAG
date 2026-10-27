using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Peis;

namespace Nap.Domain.Tests;

public class PeiTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Aluno = Guid.CreateVersion7();
    private static readonly Guid Responsavel = Guid.CreateVersion7();
    private static readonly Guid Autor = Guid.CreateVersion7();
    private static readonly Guid Aprovador = Guid.CreateVersion7();
    private static readonly Guid Curso = Guid.CreateVersion7();
    private static readonly PeriodoVigencia Vigencia = PeriodoVigencia.Criar(new DateOnly(2026, 3, 1), new DateOnly(2026, 12, 31));

    private static ConteudoPei Conteudo(string? necessidades = "Necessidades", string? demanda = "Demanda interna") =>
        new(demanda, necessidades, "Adaptações", null, null, null, Vigencia);

    private static Pei Novo(ConteudoPei? conteudo = null) =>
        Pei.Criar(Aluno, Responsavel, conteudo ?? Conteudo(), Autor, Agora);

    private static VersaoPei AguardandoAssinaturas(Pei pei)
    {
        var v = pei.Versoes.Single(x => x.Estado == EstadoVersaoPei.Rascunho);
        v.EnviarParaRevisao(Agora);
        v.AprovarRevisao(Aprovador, Agora);
        return v;
    }

    private static VersaoPei Assinada(Pei pei)
    {
        var v = AguardandoAssinaturas(pei);
        v.RegistrarAssinatura(PapelAssinatura.Profissional, Responsavel, "Profissional", v.HashConteudo!, "ev1", Agora);
        v.RegistrarAssinatura(PapelAssinatura.Aluno, null, "Aluno", v.HashConteudo!, "ev2", Agora);
        return v;
    }

    private static VersaoPei Publicada(Pei pei)
    {
        var v = Assinada(pei);
        pei.Publicar(v, Aprovador, Curso, Agora);
        return v;
    }

    [Fact]
    public void Criar_comeca_em_rascunho_versao_1()
    {
        var pei = Novo();

        var v = Assert.Single(pei.Versoes);
        Assert.Equal((Aluno, Responsavel, Autor, Agora), (pei.AlunoId, pei.ProfissionalResponsavelId, pei.CriadoPorId, pei.CriadoEm));
        Assert.Equal((1, EstadoVersaoPei.Rascunho, pei.Id, Autor), (v.Numero, v.Estado, v.PeiId, v.CriadaPorId));
        Assert.Null(v.HashConteudo);
        Assert.Same(v, pei.VersaoAberta);
        Assert.Null(pei.VersaoPublicada);
    }

    [Fact]
    public void So_rascunho_altera_conteudo()
    {
        var pei = Novo();
        var v = pei.Versoes.Single();
        var depois = Agora.AddHours(1);

        v.AtualizarConteudo(Conteudo("Outras"), depois);
        Assert.Equal("Outras", v.Conteudo.NecessidadesEducacionais);
        Assert.Equal(depois, v.AtualizadaEm);

        v.EnviarParaRevisao(Agora);
        Assert.Throws<DomainException>(() => v.AtualizarConteudo(Conteudo("Mais"), Agora));
        Assert.Equal("Outras", v.Conteudo.NecessidadesEducacionais);
    }

    [Fact]
    public void Conteudo_normaliza_e_limita_campos()
    {
        var c = new ConteudoPei("  ", " texto ", null, null, null, null, Vigencia);
        Assert.Null(c.DemandaInterna);
        Assert.Equal("texto", c.NecessidadesEducacionais);
        Assert.Throws<DomainException>(() => new ConteudoPei(null, new string('a', 4001), null, null, null, null, Vigencia));
        Assert.Throws<DomainException>(() => new ConteudoPei(new string('a', 4001), null, null, null, null, null, Vigencia));
        Assert.Throws<DomainException>(() => c with { CuidadosPedagogicos = new string('a', 4001) });
        Assert.Throws<DomainException>(() => new ConteudoPei(null, "x", null, null, null, null, null!));
    }

    [Fact]
    public void Revisao_exige_conteudo_pedagogico()
    {
        var soDemanda = new ConteudoPei("Só interna", null, null, null, null, null, Vigencia);
        Assert.False(soDemanda.TemConteudoPedagogico);
        var v = Novo(soDemanda).Versoes.Single();

        Assert.Throws<DomainException>(() => v.EnviarParaRevisao(Agora));
        Assert.Equal(EstadoVersaoPei.Rascunho, v.Estado);

        v.AtualizarConteudo(soDemanda with { RecursosApoio = "Ledor" }, Agora);
        v.EnviarParaRevisao(Agora);
        Assert.Equal(EstadoVersaoPei.EmRevisao, v.Estado);
        Assert.Equal(Agora, v.EnviadaRevisaoEm);
    }

    [Fact]
    public void Devolver_volta_a_rascunho_com_justificativa()
    {
        var v = Novo().Versoes.Single();
        Assert.Throws<DomainException>(() => v.Devolver("Ajustar", Agora)); // rascunho
        v.EnviarParaRevisao(Agora);

        Assert.Throws<DomainException>(() => v.Devolver("ab", Agora));
        Assert.Throws<DomainException>(() => v.Devolver(new string('a', 501), Agora));
        v.Devolver(" Ajustar adaptações ", Agora);

        Assert.Equal(EstadoVersaoPei.Rascunho, v.Estado);
        Assert.Equal("Ajustar adaptações", v.JustificativaDevolucao);
        Assert.Equal(Agora, v.DevolvidaEm);
    }

    [Fact]
    public void Devolver_aguardando_assinaturas_descarta_hash_e_invalida_assinaturas()
    {
        var pei = Novo();
        var v = AguardandoAssinaturas(pei);
        v.RegistrarAssinatura(PapelAssinatura.Profissional, Responsavel, "Profissional", v.HashConteudo!, "ev", Agora);
        var hashAssinado = v.HashConteudo;

        v.Devolver("Rever vigência", Agora.AddHours(1));

        Assert.Equal(EstadoVersaoPei.Rascunho, v.Estado);
        Assert.Null(v.HashConteudo);
        Assert.Null(v.AprovadaEm);
        Assert.Null(v.AprovadaPorId);
        // A assinatura fica como prova do que foi assinado, mas não vale mais.
        var invalidada = Assert.Single(v.Assinaturas);
        Assert.Equal((hashAssinado, Agora.AddHours(1)), (invalidada.HashConteudo, invalidada.InvalidadaEm));
        Assert.Null(v.AssinaturaValida(PapelAssinatura.Profissional));

        // Reaprovada, o mesmo papel assina de novo.
        v.EnviarParaRevisao(Agora.AddHours(2));
        v.AprovarRevisao(Guid.CreateVersion7(), Agora.AddHours(2));
        v.RegistrarAssinatura(PapelAssinatura.Profissional, Responsavel, "Profissional", v.HashConteudo!, "ev2", Agora.AddHours(3));
        Assert.Equal(2, v.Assinaturas.Count);
        Assert.Equal("ev2", v.AssinaturaValida(PapelAssinatura.Profissional)!.Evidencia);
    }

    [Fact]
    public void Aprovar_congela_hash()
    {
        var v = Novo().Versoes.Single();
        var hash = CalculadoraHashPei.Calcular(v.Conteudo);
        Assert.Throws<DomainException>(() => v.AprovarRevisao(Aprovador, Agora)); // rascunho
        v.EnviarParaRevisao(Agora);

        v.AprovarRevisao(Aprovador, Agora);

        Assert.Equal(EstadoVersaoPei.AguardandoAssinaturas, v.Estado);
        Assert.Equal(hash, v.HashConteudo);
        Assert.Equal((Aprovador, Agora), (v.AprovadaPorId!.Value, v.AprovadaEm!.Value));
        Assert.Throws<DomainException>(() => v.AtualizarConteudo(Conteudo("Mudou"), Agora));
    }

    [Fact]
    public void Hash_e_deterministico_e_muda_com_o_conteudo()
    {
        var hash = CalculadoraHashPei.Calcular(Conteudo());

        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.Equal(hash, CalculadoraHashPei.Calcular(Conteudo()));
        Assert.Equal(hash, CalculadoraHashPei.Calcular(Conteudo(demanda: "Outra demanda interna")));
        Assert.Equal(hash, CalculadoraHashPei.Calcular(Conteudo(demanda: null)));
        Assert.NotEqual(hash, CalculadoraHashPei.Calcular(Conteudo("Necessidades.")));
        Assert.NotEqual(hash, CalculadoraHashPei.Calcular(Conteudo() with
        {
            Vigencia = PeriodoVigencia.Criar(new DateOnly(2026, 3, 1), new DateOnly(2026, 12, 30)),
        }));
        // Campo nulo e campo vazio não colidem com o valor de outro campo.
        Assert.NotEqual(
            CalculadoraHashPei.Calcular(new ConteudoPei(null, "x", null, null, null, null, Vigencia)),
            CalculadoraHashPei.Calcular(new ConteudoPei(null, null, "x", null, null, null, Vigencia)));
    }

    [Fact]
    public void Hash_segue_o_json_canonico_documentado()
    {
        var c = new ConteudoPei("interna", "Atenção", "<b> & 'a' \"x\" + 1 😀", null, null, null,
            PeriodoVigencia.Criar(new DateOnly(2026, 3, 1), new DateOnly(2026, 12, 31)));
        const string json = "{\"necessidadesEducacionais\":\"Aten\\u00E7\\u00E3o\","
            + "\"adaptacoes\":\"\\u003Cb\\u003E \\u0026 \\u0027a\\u0027 \\u0022x\\u0022 \\u002B 1 \\uD83D\\uDE00\","
            + "\"orientacoesProfessores\":null,\"recursosApoio\":null,\"cuidadosPedagogicos\":null,"
            + "\"vigenciaInicio\":\"2026-03-01\",\"vigenciaFim\":\"2026-12-31\"}";
        var esperado = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));

        Assert.Equal(esperado, CalculadoraHashPei.Calcular(c));
    }

    [Fact]
    public void Assinatura_com_hash_diferente_e_rejeitada()
    {
        var v = AguardandoAssinaturas(Novo());

        Assert.Throws<DomainException>(() =>
            v.RegistrarAssinatura(PapelAssinatura.Aluno, null, "Aluno", new string('0', 64), "ev", Agora));
        Assert.Empty(v.Assinaturas);
    }

    [Fact]
    public void Duas_assinaturas_tornam_assinada()
    {
        var v = AguardandoAssinaturas(Novo());
        Assert.Throws<DomainException>(() =>
            v.RegistrarAssinatura(PapelAssinatura.Aluno, null, "  ", v.HashConteudo!, "ev", Agora));
        Assert.Throws<DomainException>(() =>
            v.RegistrarAssinatura(PapelAssinatura.Aluno, null, "Aluno", v.HashConteudo!, " ", Agora));

        v.RegistrarAssinatura(PapelAssinatura.Profissional, Responsavel, "Profissional", v.HashConteudo!, "ev1", Agora);
        Assert.False(v.TodasAssinaturas);
        Assert.Equal(EstadoVersaoPei.AguardandoAssinaturas, v.Estado);
        Assert.Throws<DomainException>(() =>
            v.RegistrarAssinatura(PapelAssinatura.Profissional, Responsavel, "Profissional", v.HashConteudo!, "ev", Agora));

        v.RegistrarAssinatura(PapelAssinatura.Aluno, null, " Aluno ", v.HashConteudo!, "ev2", Agora);

        Assert.True(v.TodasAssinaturas);
        Assert.Equal(EstadoVersaoPei.Assinada, v.Estado);
        var a = v.Assinaturas.Single(x => x.Papel == PapelAssinatura.Aluno);
        Assert.Equal(("Aluno", "Demonstrativa", v.HashConteudo, "ev2", Agora, (Guid?)null),
            (a.NomeSignatario, a.Metodo, a.HashConteudo, a.Evidencia, a.Em, a.SignatarioId));
    }

    [Fact]
    public void Publicar_exige_assinada()
    {
        var pei = Novo();
        var v = AguardandoAssinaturas(pei);
        Assert.Throws<DomainException>(() => pei.Publicar(v, Aprovador, Curso, Agora));
        Assert.Throws<DomainException>(() => Novo().Publicar(v, Aprovador, Curso, Agora));

        v.RegistrarAssinatura(PapelAssinatura.Profissional, Responsavel, "Profissional", v.HashConteudo!, "ev1", Agora);
        v.RegistrarAssinatura(PapelAssinatura.Aluno, null, "Aluno", v.HashConteudo!, "ev2", Agora);
        pei.Publicar(v, Aprovador, Curso, Agora);

        Assert.Equal(EstadoVersaoPei.Publicada, v.Estado);
        Assert.Equal((Agora, Aprovador, Curso), (v.PublicadaEm!.Value, v.PublicadaPorId!.Value, v.CursoPublicacaoId!.Value));
        Assert.Same(v, pei.VersaoPublicada);
        Assert.Null(pei.VersaoAberta);
        Assert.Throws<DomainException>(() => pei.Publicar(v, Aprovador, Curso, Agora));
        Assert.Equal(EstadoVersaoPei.Publicada, v.Estado);
    }

    [Fact]
    public void So_a_versao_mais_recente_e_publicada_e_arquiva_a_anterior()
    {
        var pei = Novo();
        var v1 = Publicada(pei);
        var v2 = pei.NovaVersao(Autor, Agora);
        v2.AtualizarConteudo(Conteudo("Revisadas"), Agora);
        var v2Assinada = Assinada(pei);
        Assert.Same(v2, v2Assinada);
        Assert.Same(v1, pei.VersaoPublicada);

        pei.Publicar(v2, Aprovador, Curso, Agora);

        Assert.Same(v2, pei.VersaoPublicada);
        Assert.Equal(EstadoVersaoPei.Arquivada, v1.Estado);
        Assert.Equal("Substituída pela versão 2", v1.MotivoArquivamento);
    }

    [Fact]
    public void Versao_assinada_impede_nova_versao_ate_ser_publicada()
    {
        // Sem a trava, a nova versão deixaria a assinada presa: só a mais recente publica.
        var pei = Novo();
        var v1 = Assinada(pei);

        Assert.Throws<DomainException>(() => pei.NovaVersao(Autor, Agora));
        Assert.Single(pei.Versoes);

        pei.Publicar(v1, Aprovador, Curso, Agora);
        Assert.Equal(2, pei.NovaVersao(Autor, Agora).Numero);
    }

    [Fact]
    public void Arquivar_so_publicada_com_motivo()
    {
        var pei = Novo();
        var assinada = Assinada(pei);
        Assert.Throws<DomainException>(() => assinada.Arquivar("Substituída", Agora));
        pei.Publicar(assinada, Aprovador, Curso, Agora);
        Assert.Throws<DomainException>(() => assinada.Arquivar("ab", Agora));

        assinada.Arquivar("Substituída pela versão 2", Agora);

        Assert.Equal(EstadoVersaoPei.Arquivada, assinada.Estado);
        Assert.Equal(("Substituída pela versão 2", Agora), (assinada.MotivoArquivamento, assinada.ArquivadaEm!.Value));
        Assert.Null(pei.VersaoPublicada);
        Assert.Throws<DomainException>(() => assinada.Arquivar("De novo", Agora));
    }

    [Fact]
    public void Assinada_e_publicada_sao_imutaveis()
    {
        var pei = Novo();
        var v = Assinada(pei);
        var hash = v.HashConteudo!;

        void ExigirImutavel()
        {
            Assert.Throws<DomainException>(() => v.AtualizarConteudo(Conteudo("Mudou"), Agora));
            Assert.Throws<DomainException>(() => v.EnviarParaRevisao(Agora));
            Assert.Throws<DomainException>(() => v.AprovarRevisao(Aprovador, Agora));
            Assert.Throws<DomainException>(() => v.Devolver("Ajustar", Agora));
            Assert.Throws<DomainException>(() =>
                v.RegistrarAssinatura(PapelAssinatura.Aluno, null, "Aluno", hash, "ev", Agora));
            Assert.Equal(hash, v.HashConteudo);
            Assert.Equal("Necessidades", v.Conteudo.NecessidadesEducacionais);
        }

        ExigirImutavel();
        pei.Publicar(v, Aprovador, Curso, Agora);
        ExigirImutavel();
        v.Arquivar("Encerrado", Agora);
        ExigirImutavel();
    }

    [Fact]
    public void Nova_versao_exige_nenhuma_aberta_e_zera_assinaturas()
    {
        var pei = Novo();
        Assert.Throws<DomainException>(() => pei.NovaVersao(Autor, Agora));
        var v1 = AguardandoAssinaturas(pei);
        Assert.Throws<DomainException>(() => pei.NovaVersao(Autor, Agora));
        v1.RegistrarAssinatura(PapelAssinatura.Profissional, Responsavel, "Profissional", v1.HashConteudo!, "ev1", Agora);
        v1.RegistrarAssinatura(PapelAssinatura.Aluno, null, "Aluno", v1.HashConteudo!, "ev2", Agora);
        pei.Publicar(v1, Aprovador, Curso, Agora);
        var depois = Agora.AddDays(1);
        var outroAutor = Guid.CreateVersion7();

        var v2 = pei.NovaVersao(outroAutor, depois);

        Assert.Equal((2, EstadoVersaoPei.Rascunho, pei.Id, depois, outroAutor),
            (v2.Numero, v2.Estado, v2.PeiId, v2.CriadaEm, v2.CriadaPorId));
        Assert.Equal(v1.Conteudo, v2.Conteudo);
        Assert.Empty(v2.Assinaturas);
        Assert.Empty(v2.Ciencias);
        Assert.Null(v2.HashConteudo);
        Assert.Same(v2, pei.VersaoAberta);
        Assert.Same(v1, pei.VersaoPublicada);
        Assert.Equal(2, v1.Assinaturas.Count);
        Assert.Throws<DomainException>(() => pei.NovaVersao(Autor, Agora));
    }

    [Fact]
    public void Ciencia_so_em_publicada_e_idempotente()
    {
        var pei = Novo();
        var v = Assinada(pei);
        var coord = Guid.CreateVersion7();
        Assert.Throws<DomainException>(() => v.RegistrarCiencia(coord, Agora));

        pei.Publicar(v, Aprovador, Curso, Agora);
        v.RegistrarCiencia(coord, Agora);
        v.RegistrarCiencia(coord, Agora.AddHours(1));
        v.RegistrarCiencia(Guid.CreateVersion7(), Agora);

        Assert.Equal(2, v.Ciencias.Count);
        var c = v.Ciencias.Single(x => x.CoordenadorId == coord);
        Assert.Equal(Agora, c.Em);

        v.Arquivar("Encerrado", Agora);
        Assert.Throws<DomainException>(() => v.RegistrarCiencia(Guid.CreateVersion7(), Agora));
    }

    [Fact]
    public void Descartar_so_rascunho_a_partir_da_versao_2()
    {
        var pei = Novo();
        var v1 = pei.Versoes.Single();
        Assert.Throws<DomainException>(() => pei.DescartarVersao(v1));

        Publicada(pei);
        var v2 = pei.NovaVersao(Autor, Agora);
        Assert.Throws<DomainException>(() => pei.DescartarVersao(Novo().Versoes.Single()));
        v2.EnviarParaRevisao(Agora);
        Assert.Throws<DomainException>(() => pei.DescartarVersao(v2));
        v2.Devolver("Rever", Agora);

        pei.DescartarVersao(v2);

        Assert.Same(v1, Assert.Single(pei.Versoes));
        Assert.Null(pei.VersaoAberta);
        Assert.Equal(2, pei.NovaVersao(Autor, Agora).Numero);
    }

    [Fact]
    public void Trocar_responsavel_atualiza_o_pei()
    {
        var pei = Novo();
        var v = AguardandoAssinaturas(pei);
        v.RegistrarAssinatura(PapelAssinatura.Profissional, Responsavel, "Profissional", v.HashConteudo!, "ev1", Agora);
        var novo = Guid.CreateVersion7();

        pei.TrocarResponsavel(novo, "  Licença médica ", Autor, Agora);

        Assert.Equal(novo, pei.ProfissionalResponsavelId);
        Assert.Equal(new TrocaResponsavelPei(Responsavel, novo, "Licença médica", Autor, Agora), Assert.Single(pei.TrocasResponsavel));
        Assert.Equal(EstadoVersaoPei.AguardandoAssinaturas, v.Estado);
        Assert.Equal(Responsavel, Assert.Single(v.Assinaturas).SignatarioId);
        Assert.Throws<DomainException>(() => pei.TrocarResponsavel(novo, "Licença médica", Autor, Agora));
        Assert.Throws<DomainException>(() => pei.TrocarResponsavel(Guid.Empty, "Licença médica", Autor, Agora));
        Assert.Throws<DomainException>(() => pei.TrocarResponsavel(Guid.CreateVersion7(), " x ", Autor, Agora));
        Assert.Throws<DomainException>(() => pei.TrocarResponsavel(Guid.CreateVersion7(), new string('a', 501), Autor, Agora));
        Assert.Single(pei.TrocasResponsavel);
    }
}
