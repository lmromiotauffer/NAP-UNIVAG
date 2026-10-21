using Nap.Application.Comum;

namespace Nap.Application.Tests;

public class ValidacaoArquivoTests
{
    private const long Limite = 5_242_880;

    private static ValidationException Rejeitar(string nome, byte[] conteudo, long limite = Limite) =>
        Assert.Throws<ValidationException>(() => ValidacaoArquivo.Validar(nome, conteudo, limite));

    private static string Mensagem(ValidationException ex) => Assert.Single(ex.Erros["arquivo"]);

    [Theory]
    [InlineData("laudo.pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }, ".pdf", "application/pdf")]
    [InlineData("foto.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 }, ".png", "image/png")]
    [InlineData("foto.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ".jpg", "image/jpeg")]
    [InlineData("foto.jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ".jpeg", "image/jpeg")]
    [InlineData("relatorio.docx", new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14 },
        ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("planilha.xlsx", new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14 },
        ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("notas.txt", new byte[] { 0x4F, 0x6C, 0xC3, 0xA1 }, ".txt", "text/plain; charset=utf-8")]
    [InlineData("bom.txt", new byte[] { 0xEF, 0xBB, 0xBF, 0x61 }, ".txt", "text/plain; charset=utf-8")]
    [InlineData("dados.csv", new byte[] { 0x61, 0x3B, 0x62, 0x0A }, ".csv", "text/csv; charset=utf-8")]
    public void Formato_aceito_com_assinatura_valida(string nome, byte[] conteudo, string extensao, string tipo)
    {
        Assert.Equal(new ResultadoArquivo(extensao, tipo), ValidacaoArquivo.Validar(nome, conteudo, Limite));
    }

    [Fact]
    public void Pdf_com_conteudo_de_executavel_e_rejeitado()
    {
        var ex = Rejeitar("laudo.pdf", [0x4D, 0x5A, 0x90, 0x00]);
        Assert.Equal("O conteúdo do arquivo não corresponde ao formato PDF.", Mensagem(ex));
    }

    [Fact]
    public void Extensao_nao_aceita_e_rejeitada()
    {
        var ex = Rejeitar("programa.exe", [0x4D, 0x5A]);
        Assert.Equal("Formato não aceito. Envie PDF, PNG, JPG, DOCX, XLSX, TXT ou CSV.", Mensagem(ex));
    }

    [Fact]
    public void Arquivo_vazio_e_rejeitado()
    {
        Assert.Equal("Envie um arquivo.", Mensagem(Rejeitar("laudo.pdf", [])));
    }

    [Fact]
    public void Acima_do_limite_e_rejeitado()
    {
        var conteudo = new byte[Limite + 1];
        "%PDF-"u8.CopyTo(conteudo);
        Assert.Equal("O arquivo excede o limite de 5 MiB.", Mensagem(Rejeitar("laudo.pdf", conteudo)));
        // No limite exato, passa.
        Assert.Equal(".pdf", ValidacaoArquivo.Validar("laudo.pdf", conteudo.AsSpan(0, (int)Limite), Limite).Extensao);
    }

    [Fact]
    public void Txt_com_utf8_invalido_e_rejeitado()
    {
        var ex = Rejeitar("notas.txt", [0x61, 0xC3, 0x28]);
        Assert.Equal("O conteúdo do arquivo não corresponde ao formato TXT.", Mensagem(ex));
    }

    [Fact]
    public void Txt_com_byte_nulo_e_rejeitado()
    {
        var ex = Rejeitar("notas.txt", [0x61, 0x00, 0x62]);
        Assert.Equal("O conteúdo do arquivo não corresponde ao formato TXT.", Mensagem(ex));
    }

    [Fact]
    public void Extensao_em_maiusculas_e_aceita()
    {
        Assert.Equal(new ResultadoArquivo(".pdf", "application/pdf"), ValidacaoArquivo.Validar("LAUDO.PDF", "%PDF-1.7"u8, Limite));
    }
}
