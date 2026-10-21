using Nap.Domain.Documentos;

namespace Nap.Application.Features.Documentos;

/// <summary><c>CompartilhadoPor</c>/<c>CompartilhadoEm</c> só vêm preenchidos enquanto o documento está compartilhado.</summary>
public record DocumentoDto(Guid Id, CategoriaDocumento Categoria, string Descricao, string NomeArquivo, long TamanhoBytes,
    string Autor, DateTimeOffset CriadoEm, VisibilidadeDocumento Visibilidade, string? CompartilhadoPor,
    DateTimeOffset? CompartilhadoEm, bool Arquivado, string Versao);

/// <summary>O chamador é dono do <c>Conteudo</c> e deve descartá-lo.</summary>
public record ArquivoParaDownload(Stream Conteudo, string NomeArquivo, string TipoConteudo);
