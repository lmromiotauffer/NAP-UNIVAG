using Nap.Application.Comum;
using Nap.Application.Features.Peis;
using Nap.Application.Features.Peis.Queries;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Comum;
using Nap.Domain.Peis;

namespace Nap.Application.Tests;

public class PdfPeiTests
{
    private const string DemandaDeTeste = "Demanda sigilosa de teste";
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly UnidadeDeTrabalhoFalsa _uow = new();

    private static VersaoPeiDetalheDto Detalhe(string? demanda = DemandaDeTeste) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Maria", "2024001", "Direito", "Ana", 2, EstadoVersaoPei.Publicada,
        new ConteudoPeiDto(demanda, "Necessidades", "Adaptações", "Orientações", "Recursos", "Cuidados",
            new DateOnly(2026, 8, 1), new DateOnly(2027, 7, 31)),
        new string('a', 64),
        [new AssinaturaDto(PapelAssinatura.Profissional, "Ana", DateTimeOffset.UnixEpoch, "Demonstrativa"),
            new AssinaturaDto(PapelAssinatura.Aluno, "Maria", DateTimeOffset.UnixEpoch, "Demonstrativa")],
        [new CienciaDto("Coord", DateTimeOffset.UnixEpoch)], DateTimeOffset.UnixEpoch, "Coordenação",
        new AcoesVersaoDto(false, false, false, false, false, false, false, false, false, false), "AAAAAAAAB9E=", false);

    [Fact]
    public void Modelo_pedagogico_omite_demanda_interna()
    {
        var m = ModeloPdfPei.Montar(Detalhe(), VariantePdf.Pedagogica);

        Assert.Equal(VariantePdf.Pedagogica, m.Variante);
        Assert.DoesNotContain(m.Secoes, s => s.Titulo.Contains("Demanda interna"));
        Assert.DoesNotContain(m.Secoes, s => s.Texto.Contains(DemandaDeTeste));
        Assert.Equal(5, m.Secoes.Count);
    }

    [Fact]
    public void Modelo_completo_inclui_todas_as_secoes()
    {
        var m = ModeloPdfPei.Montar(Detalhe(), VariantePdf.Completa);

        Assert.Equal(6, m.Secoes.Count);
        var interna = Assert.Single(m.Secoes, s => s.Titulo.Contains("Demanda interna"));
        Assert.Contains("Uso interno do NAP — não faz parte do documento assinado", interna.Titulo);
        Assert.Equal(DemandaDeTeste, interna.Texto);
        Assert.Contains(m.Secoes, s => s.Texto == "Cuidados");
        Assert.Equal(("Maria", "2024001", "Direito", "Ana", 2), (m.Aluno, m.Matricula, m.Curso, m.Responsavel, m.Numero));
        Assert.Equal(2, m.Assinaturas.Count);
        Assert.Single(m.Ciencias);
    }

    [Theory]
    [InlineData(EstadoVersaoPei.Rascunho, "Rascunho")]
    [InlineData(EstadoVersaoPei.EmRevisao, "Em revisão")]
    [InlineData(EstadoVersaoPei.AguardandoAssinaturas, "Aguardando assinaturas")]
    [InlineData(EstadoVersaoPei.Publicada, "Publicada")]
    public void Modelo_leva_o_estado_da_versao(EstadoVersaoPei estado, string rotulo)
    {
        var m = ModeloPdfPei.Montar(Detalhe() with { Estado = estado }, VariantePdf.Completa);
        Assert.Equal(rotulo, m.Estado);
    }

    [Fact]
    public void Assinaturas_levam_o_aviso_de_nao_certificada()
    {
        var m = ModeloPdfPei.Montar(Detalhe(), VariantePdf.Pedagogica);
        Assert.Equal("Assinatura demonstrativa — não certificada (pendência institucional)", m.AvisoAssinatura);
    }

    private sealed class GeradorFalso : IGeradorPdfPei
    {
        public ModeloPdfPei? Recebido { get; private set; }

        public byte[] Gerar(ModeloPdfPei m)
        {
            Recebido = m;
            return [1, 2, 3];
        }
    }

    private sealed class PeiQueriesFalso(VersaoPeiDetalheDto? detalhe) : IPeiQueries
    {
        public Task<IReadOnlyList<PeiResumoDto>?> ListarDoAlunoAsync(Guid alunoId, EscopoPeis e, Guid usuarioId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PeiResumoDto>?>(null);

        public Task<VersaoPeiDetalheDto?> ObterVersaoAsync(Guid versaoId, EscopoPeis e, IUsuarioAtual u, CancellationToken ct) =>
            Task.FromResult(detalhe);
    }

    [Theory]
    [InlineData(Perfis.CoordenadorCurso, VariantePdf.Pedagogica)]
    [InlineData(Perfis.ProfissionalNap, VariantePdf.Completa)]
    public async Task Pdf_escolhe_a_variante_pelo_escopo_audita_e_nomeia_pela_matricula(string perfil, VariantePdf esperada)
    {
        var gerador = new GeradorFalso();
        var usuario = new UsuarioAtualFalso(perfil, Guid.NewGuid());
        var h = new BaixarPdfPeiHandler(new BaixarPdfPeiValidator(), new PeiQueriesFalso(Detalhe()), gerador, usuario, _auditoria, _uow);

        var arquivo = await h.Handle(new BaixarPdfPeiQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(esperada, gerador.Recebido!.Variante);
        Assert.Equal("PEI-2024001-v2.pdf", arquivo.NomeArquivo);
        Assert.Equal("application/pdf", arquivo.TipoConteudo);
        Assert.Equal(3, arquivo.Conteudo.Length);
        Assert.Equal("pei.pdf", Assert.Single(_auditoria.Registros).Acao);
        Assert.Equal(1, _uow.Salvamentos);
    }

    [Fact]
    public async Task Pdf_fora_do_escopo_gera_404_sem_auditar()
    {
        var h = new BaixarPdfPeiHandler(new BaixarPdfPeiValidator(), new PeiQueriesFalso(null), new GeradorFalso(),
            new UsuarioAtualFalso(Perfis.CoordenadorNap), _auditoria, _uow);
        await Assert.ThrowsAsync<NotFoundException>(() => h.Handle(new BaixarPdfPeiQuery(Guid.NewGuid()), TestContext.Current.CancellationToken));
        Assert.Empty(_auditoria.Registros);
    }
}
