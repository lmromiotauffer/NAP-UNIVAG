using System.Text;
using Nap.Application.Features.Peis;
using Nap.Domain.Peis;
using Nap.Infrastructure.Comum;
using Nap.Infrastructure.Pdf;

namespace Nap.Infrastructure.Tests;

public class GeradorPdfPeiTests
{
    private static ModeloPdfPei Modelo(VariantePdf variante) => new(variante, "João Conceição", "2024001", "Educação Física",
        "Ana Lúcia", 3, "Rascunho", new DateOnly(2026, 8, 1), new DateOnly(2027, 7, 31),
        [("Necessidades educacionais", "Atenção à leitura; ação, avaliação diferenciada — ç ã õ é ê í ó ú."),
            ("Demanda interna (Uso interno do NAP — não faz parte do documento assinado)", "Observação reservada.")],
        [new AssinaturaDto(PapelAssinatura.Profissional, "Ana Lúcia", DateTimeOffset.UnixEpoch, "Demonstrativa")],
        new string('f', 64), DateTimeOffset.UnixEpoch, [new CienciaDto("Coordenação de Educação Física", DateTimeOffset.UnixEpoch)]);

    private static readonly GeradorPdfPei Gerador = new(new Relogio());

    [Fact]
    public void Pdf_gerado_comeca_com_assinatura_pdf_e_tem_conteudo()
    {
        var bytes = Gerador.Gerar(Modelo(VariantePdf.Pedagogica));

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.True(bytes.Length > 1024, $"PDF com {bytes.Length} bytes");
    }

    [Fact]
    public void Pdf_gera_com_acentos_sem_excecao()
    {
        var bytes = Gerador.Gerar(Modelo(VariantePdf.Completa));

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        // A fonte embarcada é a Liberation Sans, não uma do sistema.
        Assert.Contains("+Liberation#20Sans", Encoding.Latin1.GetString(bytes));
    }
}
