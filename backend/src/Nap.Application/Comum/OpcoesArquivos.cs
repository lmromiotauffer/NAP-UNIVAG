namespace Nap.Application.Comum;

/// <summary>Armazenamento de arquivos (seção <c>Nap:Arquivos</c>). O diretório é relativo ao content root.</summary>
public sealed class OpcoesArquivos
{
    public string Diretorio { get; set; } = string.Empty;
    public long LimiteBytes { get; set; } = 5_242_880;
}
