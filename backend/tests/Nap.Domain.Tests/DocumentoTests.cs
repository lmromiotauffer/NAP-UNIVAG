using Nap.Domain.Comum;
using Nap.Domain.Documentos;

namespace Nap.Domain.Tests;

public class DocumentoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Autor = Guid.CreateVersion7();

    private static Documento Novo(string nome = "laudo.pdf") =>
        Documento.Criar(Guid.CreateVersion7(), Guid.CreateVersion7(), CategoriaDocumento.Laudo, "Laudo do aluno",
            nome, "application/pdf", 1024, new string('a', 64), Guid.CreateVersion7().ToString("N"), Autor, Agora);

    [Fact]
    public void Nasce_restrito_ao_nap()
    {
        var d = Novo();
        Assert.Equal(VisibilidadeDocumento.RestritoNap, d.Visibilidade);
        Assert.False(d.Arquivado);
        Assert.Null(d.CompartilhadoEm);
    }

    [Fact]
    public void Trocar_categoria_nao_compartilha()
    {
        var d = Novo();
        d.AlterarCategoria(CategoriaDocumento.Relatorio);
        Assert.Equal(CategoriaDocumento.Relatorio, d.Categoria);
        Assert.Equal(VisibilidadeDocumento.RestritoNap, d.Visibilidade);

        d.Compartilhar("Necessário ao curso", Autor, Agora);
        d.AlterarCategoria(CategoriaDocumento.Laudo);
        Assert.Equal(VisibilidadeDocumento.CompartilhadoCoordenacao, d.Visibilidade);
    }

    [Fact]
    public void Compartilhar_exige_justificativa_e_registra_autor()
    {
        var d = Novo();
        Assert.Throws<DomainException>(() => d.Compartilhar("  ", Autor, Agora));
        Assert.Equal(VisibilidadeDocumento.RestritoNap, d.Visibilidade);

        d.Compartilhar(" Necessário ao curso ", Autor, Agora);

        Assert.Equal(VisibilidadeDocumento.CompartilhadoCoordenacao, d.Visibilidade);
        Assert.Equal(Autor, d.CompartilhadoPorId);
        Assert.Equal(Agora, d.CompartilhadoEm);
        Assert.Equal("Necessário ao curso", d.CompartilhadoJustificativa);
        Assert.Throws<DomainException>(() => d.Compartilhar("Outra vez", Autor, Agora));
    }

    [Fact]
    public void Revogar_volta_a_restrito()
    {
        var d = Novo();
        Assert.Throws<DomainException>(() => d.RevogarCompartilhamento("Motivo", Autor, Agora));
        d.Compartilhar("Necessário ao curso", Autor, Agora);

        d.RevogarCompartilhamento("Não é mais necessário", Autor, Agora.AddDays(1));

        Assert.Equal(VisibilidadeDocumento.RestritoNap, d.Visibilidade);
        Assert.Equal(Autor, d.RevogadoPorId);
        Assert.Equal(Agora.AddDays(1), d.RevogadoEm);
        Assert.Equal("Não é mais necessário", d.RevogadoJustificativa);
    }

    [Fact]
    public void Arquivado_nao_aceita_mais_operacoes()
    {
        var d = Novo();
        Assert.Throws<DomainException>(() => d.Arquivar("ab", Autor, Agora));
        d.Arquivar("Enviado por engano", Autor, Agora);

        Assert.True(d.Arquivado);
        Assert.Equal("Enviado por engano", d.ArquivadoMotivo);
        Assert.Equal(Autor, d.ArquivadoPorId);
        Assert.Throws<DomainException>(() => d.AlterarCategoria(CategoriaDocumento.Termo));
        Assert.Throws<DomainException>(() => d.Compartilhar("Justificativa", Autor, Agora));
        Assert.Throws<DomainException>(() => d.RevogarCompartilhamento("Justificativa", Autor, Agora));
        Assert.Throws<DomainException>(() => d.Arquivar("Motivo", Autor, Agora));
    }

    [Theory]
    [InlineData("..\\..\\x.pdf", "x.pdf")]
    [InlineData("../../etc/y.pdf", "y.pdf")]
    [InlineData("a\u0000b\n.txt", "ab.txt")]
    [InlineData("   ", "arquivo")]
    public void Nome_de_arquivo_sem_caminho(string bruto, string esperado) =>
        Assert.Equal(esperado, Novo(bruto).NomeArquivo);

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".docx")]
    public void Nome_longo_preserva_extensao(string ext)
    {
        var nome = Novo(new string('a', 300) + ext).NomeArquivo;
        Assert.True(nome.Length <= 200);
        Assert.EndsWith(ext, nome);
    }

    [Fact]
    public void Nome_longo_nao_corta_par_substituto()
    {
        var nome = Novo(new string('a', 195) + "😀😀.pdf").NomeArquivo;
        Assert.True(nome.Length <= 200);
        Assert.EndsWith(".pdf", nome);
        Assert.False(char.IsHighSurrogate(nome[^5]));
    }
}
